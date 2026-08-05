using AutoMapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Kurumlar.Entities;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.CariKartlar.Mapping;
using STYS.Muhasebe.CariKartlar.Repositories;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.MuhasebeDonemleri.Mapping;
using STYS.Muhasebe.MuhasebeDonemleri.Repositories;
using STYS.Muhasebe.MuhasebeDonemleri.Services;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Muhasebe.SatisBelgeleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using STYS.Muhasebe.SatisBelgeleri.Mapping;
using STYS.Muhasebe.SatisBelgeleri.Repositories;
using STYS.Muhasebe.SatisBelgeleri.Services;
using STYS.Muhasebe.SatisBelgeleri.Services.MuhasebeFisStratejileri;
using STYS.Tesisler.Entities;
using TOD.Platform.AspNetCore.Logging;
using TOD.Platform.SharedKernel.Exceptions;
using Xunit;

namespace STYS.Tests;

[Trait("Category", "Integration")]
[Collection(SqlServerIntegrationCollection.Name)]
public class EBelgeOutboxLeaseTransitionIntegrationTests : IAsyncLifetime
{
    private const string TestMarker = "EBO-2A3";
    private const int ClaimLeaseSeconds = 30;
    private const int RetryDelaySeconds = 45;
    private const int RenewLeaseSeconds = 60;

    private string _uniqueSuffix = TestMarker;
    private int _kurumId;
    private int _ilId;
    private int _tesisId;
    private int _musteriKartId;
    private int _tedarikciKartId;

    public async Task InitializeAsync()
    {
        _uniqueSuffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];

        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var (kurum, il, tesis) = await SatisBelgesiMuhasebeTestSupport.SeedKurumIlTesisAsync(dbContext, _uniqueSuffix);
        _kurumId = kurum.Id;
        _ilId = il.Id;
        _tesisId = tesis.Id;

        var musteriHesap = SatisBelgesiMuhasebeTestSupport.BuildHesap(_uniqueSuffix, "MUS", _tesisId);
        var tedarikciHesap = SatisBelgesiMuhasebeTestSupport.BuildHesap(_uniqueSuffix, "TED", _tesisId);
        var gelirHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(_uniqueSuffix, MuhasebeAnaHesapKodlari.GelirSatis, "GELIR", _tesisId);
        var kdvHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(_uniqueSuffix, MuhasebeAnaHesapKodlari.KDVHesaplanan, "KDV", _tesisId);
        dbContext.MuhasebeHesapPlanlari.AddRange(musteriHesap, tedarikciHesap, gelirHesap, kdvHesap);
        await dbContext.SaveChangesAsync();

        var musteriKart = SatisBelgesiMuhasebeTestSupport.BuildCariKart(_uniqueSuffix, "MUS", CariKartTipleri.Musteri, _tesisId, musteriHesap.Id);
        musteriKart.EArsivKapsamindaMi = true;
        var tedarikciKart = SatisBelgesiMuhasebeTestSupport.BuildCariKart(_uniqueSuffix, "TED", CariKartTipleri.Tedarikci, _tesisId, tedarikciHesap.Id);
        tedarikciKart.VergiNoTckn = "1111111111";
        tedarikciKart.EFaturaMukellefiMi = true;
        dbContext.CariKartlar.AddRange(musteriKart, tedarikciKart);
        await dbContext.SaveChangesAsync();
        _musteriKartId = musteriKart.Id;
        _tedarikciKartId = tedarikciKart.Id;

        dbContext.MuhasebeDonemler.Add(new STYS.Muhasebe.MuhasebeDonemleri.Entities.MuhasebeDonem
        {
            TesisId = _tesisId,
            MaliYil = 2026,
            DonemNo = 1,
            BaslangicTarihi = new DateTime(2026, 1, 1),
            BitisTarihi = new DateTime(2026, 12, 31),
            KapaliMi = false
        });

        dbContext.KurumFaturaNumaraSayaclari.Add(new KurumFaturaNumaraSayaci
        {
            KurumId = _kurumId,
            MaliYil = 2026,
            SeriKodu = "EBF",
            SonNumara = 0,
            AktifMi = true
        });

        await dbContext.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        await dbContext.KurumFaturaNumaraSayaclari.Where(x => x.KurumId == _kurumId).ExecuteDeleteAsync();
        await SatisBelgesiMuhasebeTestSupport.CleanupAsync(dbContext, _uniqueSuffix, _tesisId, _kurumId, _ilId);
    }

    private static IMapper CreateMapper()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<SatisBelgesiProfile>();
            cfg.AddProfile<MuhasebeDonemProfile>();
            cfg.AddProfile<CariKartProfile>();
        }, NullLoggerFactory.Instance);

        return config.CreateMapper();
    }

    private static ISatisBelgesiService CreateService(StysAppDbContext dbContext)
    {
        var mapper = CreateMapper();
        var satisBelgesiRepository = new SatisBelgesiRepository(dbContext, mapper);
        var muhasebeFisRepository = new STYS.Muhasebe.MuhasebeFisleri.Repositories.MuhasebeFisRepository(dbContext, mapper);
        return new SatisBelgesiService(
            satisBelgesiRepository,
            dbContext,
            mapper,
            muhasebeFisRepository,
            null!,
            new SatisBelgesiMuhasebeTestSupport.FakeUserAccessScopeService(),
            NullLogger<SatisBelgesiService>.Instance,
            new SatisBelgesiMuhasebeTestSupport.NoOpDomainOperationLogger());
    }

    private static IEBelgeOutboxClaimLeaseService CreateClaimService(StysAppDbContext dbContext)
        => new EBelgeOutboxClaimLeaseService(dbContext);

    private static IEBelgeOutboxLeaseTransitionService CreateTransitionService(StysAppDbContext dbContext)
        => new EBelgeOutboxLeaseTransitionService(dbContext);

    private static string TruncateToMax(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];

    private CreateSatisBelgesiRequest BuildSatisBelgesiRequest(SatisBelgesiTipi belgeTipi = SatisBelgesiTipi.SatisFaturasi)
        => new()
        {
            BelgeNo = TruncateToMax($"{_uniqueSuffix}-EBF-{Guid.NewGuid():N}", 40),
            BelgeTipi = belgeTipi,
            TesisId = _tesisId,
            CariKartId = belgeTipi == SatisBelgesiTipi.AlisFaturasi ? _tedarikciKartId : _musteriKartId,
            KarsiTarafFaturaNo = belgeTipi == SatisBelgesiTipi.AlisFaturasi ? TruncateToMax($"KTF-{_uniqueSuffix}", 40) : null,
            BelgeTarihi = new DateTime(2026, 3, 1),
            Satirlar =
            [
                new CreateSatisBelgesiSatiriRequest
                {
                    SiraNo = 1,
                    Aciklama = "Test satiri",
                    SatirTipi = SatisBelgesiSatirTipi.EkHizmet,
                    Miktar = 1,
                    BirimFiyat = 1000m,
                    KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli,
                    KdvOrani = 20m
                }
            ]
        };

    private async Task EnsureMuhasebeFisIdAsync(int satisBelgesiId)
    {
        await using var verifyCtx = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var muhasebeFisId = await verifyCtx.SatisBelgeleri
            .AsNoTracking()
            .Where(x => x.Id == satisBelgesiId)
            .Select(x => x.MuhasebeFisId)
            .SingleAsync();

        Assert.True(muhasebeFisId.HasValue);
    }

    private async Task<int> CreateOutgoingInvoiceAsync()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = CreateService(dbContext);

        var created = await service.CreateAsync(BuildSatisBelgesiRequest());
        await service.MuhasebeOnayinaGonderAsync(created.Id!.Value, CancellationToken.None);
        await service.MuhasebeOnaylaAsync(created.Id.Value, CancellationToken.None);

        var donemService = SatisBelgesiMuhasebeTestSupport.CreateRealMuhasebeDonemService(dbContext);
        var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext, donemService);
        await fisService.MuhasebeFisiOlusturAsync(created.Id.Value, CancellationToken.None);
        await EnsureMuhasebeFisIdAsync(created.Id.Value);

        await using var kesimCtx = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var kesimService = CreateService(kesimCtx);
        await kesimService.FaturaKesAsync(created.Id.Value, new FaturaKesRequest { SeriKodu = "EBF" }, CancellationToken.None);
        return created.Id.Value;
    }

    private async Task<ClaimedOutboxContext> ClaimOutboxAsync(int satisBelgesiId)
    {
        await using var claimCtx = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var claimService = CreateClaimService(claimCtx);
        var claim = await claimService.TryClaimNextAsync(TimeSpan.FromSeconds(ClaimLeaseSeconds), CancellationToken.None);
        Assert.NotNull(claim);
        var outbox = await GetOutboxAsync(claim!.OutboxMesajiId);
        Assert.Equal(satisBelgesiId, outbox.EBelgeKaydi.SatisBelgesiId);

        return new ClaimedOutboxContext(
            satisBelgesiId,
            claim.OutboxMesajiId,
            claim.KurumId,
            claim.EBelgeKaydiId,
            claim.KilitToken,
            claim.KilitBitisZamaniUtc,
            claim.DenemeSayisi);
    }

    private async Task<ClaimedOutboxContext> ClaimAndGetContextAsync()
    {
        var satisBelgesiId = await CreateOutgoingInvoiceAsync();
        return await ClaimOutboxAsync(satisBelgesiId);
    }

    private async Task UpdateOutboxAsync(int outboxMesajiId, Action<EBelgeOutboxMesaji> mutator)
    {
        await using var updateCtx = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var outbox = await updateCtx.EBelgeOutboxMesajlari
            .IgnoreQueryFilters()
            .SingleAsync(x => x.Id == outboxMesajiId);

        mutator(outbox);
        await updateCtx.SaveChangesAsync();
    }

    private async Task<EBelgeOutboxMesaji> GetOutboxAsync(int outboxMesajiId)
    {
        await using var verifyCtx = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        return await verifyCtx.EBelgeOutboxMesajlari
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(x => x.EBelgeKaydi)
            .SingleAsync(x => x.Id == outboxMesajiId);
    }

    private static async Task<DateTime> GetDatabaseUtcAsync()
    {
        var connectionString = SatisBelgesiMuhasebeTestSupport.ConnectionString
            ?? throw new InvalidOperationException("Connection string bulunamadi.");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT SYSUTCDATETIME();";
        return (DateTime)await command.ExecuteScalarAsync();
    }

    [IntegrationFact]
    public async Task GecerliTokenVeAktifLeaseIleCompleteBasariliOlur()
    {
        var context = await ClaimAndGetContextAsync();
        await UpdateOutboxAsync(context.OutboxMesajiId, outbox =>
        {
            outbox.SonHataKodu = "E100";
            outbox.SonHataMesaji = "Eski hata";
        });

        var before = await GetDatabaseUtcAsync();
        var transition = CreateTransitionService(SatisBelgesiMuhasebeTestSupport.CreateDbContext());
        var result = await transition.TryCompleteAsync(context.OutboxMesajiId, context.KurumId, context.KilitToken, CancellationToken.None);
        var after = await GetDatabaseUtcAsync();

        Assert.True(result);

        var outbox = await GetOutboxAsync(context.OutboxMesajiId);
        Assert.Equal(EBelgeOutboxDurumu.Tamamlandi, outbox.Durum);
        Assert.NotNull(outbox.TamamlanmaZamaniUtc);
        Assert.Null(outbox.KilitToken);
        Assert.Null(outbox.KilitBitisZamaniUtc);
        Assert.Null(outbox.SonrakiDenemeZamaniUtc);
        Assert.Null(outbox.SonHataKodu);
        Assert.Null(outbox.SonHataMesaji);
        Assert.Equal(1, outbox.DenemeSayisi);
        Assert.True(outbox.TamamlanmaZamaniUtc >= before);
        Assert.True(outbox.TamamlanmaZamaniUtc <= after);
    }

    [IntegrationFact]
    public async Task YanlisTokenCompleteYapamazVeHicbirAlanDegismez()
    {
        var context = await ClaimAndGetContextAsync();
        var before = await GetOutboxAsync(context.OutboxMesajiId);
        var transition = CreateTransitionService(SatisBelgesiMuhasebeTestSupport.CreateDbContext());

        var result = await transition.TryCompleteAsync(context.OutboxMesajiId, context.KurumId, Guid.NewGuid().ToString("D"), CancellationToken.None);

        Assert.False(result);
        var after = await GetOutboxAsync(context.OutboxMesajiId);
        Assert.Equal(before.Durum, after.Durum);
        Assert.Equal(before.KilitToken, after.KilitToken);
        Assert.Equal(before.KilitBitisZamaniUtc, after.KilitBitisZamaniUtc);
        Assert.Equal(before.DenemeSayisi, after.DenemeSayisi);
        Assert.Equal(before.SonrakiDenemeZamaniUtc, after.SonrakiDenemeZamaniUtc);
        Assert.Equal(before.SonHataKodu, after.SonHataKodu);
        Assert.Equal(before.SonHataMesaji, after.SonHataMesaji);
    }

    [IntegrationFact]
    public async Task SuresiDolmusLeaseCompleteYapamaz()
    {
        var context = await ClaimAndGetContextAsync();
        await UpdateOutboxAsync(context.OutboxMesajiId, outbox => outbox.KilitBitisZamaniUtc = DateTime.UtcNow.AddMinutes(-5));

        var before = await GetOutboxAsync(context.OutboxMesajiId);
        var transition = CreateTransitionService(SatisBelgesiMuhasebeTestSupport.CreateDbContext());

        var result = await transition.TryCompleteAsync(context.OutboxMesajiId, context.KurumId, context.KilitToken, CancellationToken.None);

        Assert.False(result);
        var after = await GetOutboxAsync(context.OutboxMesajiId);
        Assert.Equal(before.KilitBitisZamaniUtc, after.KilitBitisZamaniUtc);
        Assert.Equal(before.Durum, after.Durum);
    }

    [IntegrationFact]
    public async Task AyniTokenIleIkinciCompleteCagrisiFalseDoner()
    {
        var context = await ClaimAndGetContextAsync();
        var transition = CreateTransitionService(SatisBelgesiMuhasebeTestSupport.CreateDbContext());

        var first = await transition.TryCompleteAsync(context.OutboxMesajiId, context.KurumId, context.KilitToken, CancellationToken.None);
        var afterFirst = await GetOutboxAsync(context.OutboxMesajiId);
        var second = await transition.TryCompleteAsync(context.OutboxMesajiId, context.KurumId, context.KilitToken, CancellationToken.None);
        var afterSecond = await GetOutboxAsync(context.OutboxMesajiId);

        Assert.True(first);
        Assert.False(second);
        Assert.Equal(afterFirst.Durum, afterSecond.Durum);
        Assert.Equal(afterFirst.TamamlanmaZamaniUtc, afterSecond.TamamlanmaZamaniUtc);
        Assert.Equal(afterFirst.KilitToken, afterSecond.KilitToken);
    }

    [IntegrationFact]
    public async Task RetryDelayIleFailDurumHataVeGelecekteRetryYazar()
    {
        var context = await ClaimAndGetContextAsync();
        var before = await GetDatabaseUtcAsync();
        var transition = CreateTransitionService(SatisBelgesiMuhasebeTestSupport.CreateDbContext());

        var result = await transition.TryFailAsync(
            context.OutboxMesajiId,
            context.KurumId,
            context.KilitToken,
            "E42",
            "Gerçek hata",
            TimeSpan.FromSeconds(RetryDelaySeconds),
            CancellationToken.None);
        var after = await GetDatabaseUtcAsync();

        Assert.True(result);
        var outbox = await GetOutboxAsync(context.OutboxMesajiId);
        Assert.Equal(EBelgeOutboxDurumu.Hata, outbox.Durum);
        Assert.Equal("E42", outbox.SonHataKodu);
        Assert.Equal("Gerçek hata", outbox.SonHataMesaji);
        Assert.Null(outbox.KilitToken);
        Assert.Null(outbox.KilitBitisZamaniUtc);
        Assert.Null(outbox.TamamlanmaZamaniUtc);
        Assert.Equal(1, outbox.DenemeSayisi);
        Assert.NotNull(outbox.SonrakiDenemeZamaniUtc);
        Assert.True(outbox.SonrakiDenemeZamaniUtc >= before.AddSeconds(RetryDelaySeconds));
        Assert.True(outbox.SonrakiDenemeZamaniUtc <= after.AddSeconds(RetryDelaySeconds));
    }

    [IntegrationFact]
    public async Task RetryDelayNullIleTerminalFailDurumHataVeRetryBosOlur()
    {
        var context = await ClaimAndGetContextAsync();
        var transition = CreateTransitionService(SatisBelgesiMuhasebeTestSupport.CreateDbContext());

        var result = await transition.TryFailAsync(
            context.OutboxMesajiId,
            context.KurumId,
            context.KilitToken,
            "E41",
            "Terminal hata",
            null,
            CancellationToken.None);

        Assert.True(result);
        var outbox = await GetOutboxAsync(context.OutboxMesajiId);
        Assert.Equal(EBelgeOutboxDurumu.Hata, outbox.Durum);
        Assert.Equal("E41", outbox.SonHataKodu);
        Assert.Equal("Terminal hata", outbox.SonHataMesaji);
        Assert.Null(outbox.SonrakiDenemeZamaniUtc);
        Assert.Null(outbox.KilitToken);
        Assert.Null(outbox.KilitBitisZamaniUtc);
    }

    [IntegrationFact]
    public async Task YanlisTokenFailYapamazVeHicbirAlanDegismez()
    {
        var context = await ClaimAndGetContextAsync();
        var before = await GetOutboxAsync(context.OutboxMesajiId);
        var transition = CreateTransitionService(SatisBelgesiMuhasebeTestSupport.CreateDbContext());

        var result = await transition.TryFailAsync(
            context.OutboxMesajiId,
            context.KurumId,
            Guid.NewGuid().ToString("D"),
            "E42",
            "Gerçek hata",
            TimeSpan.FromSeconds(RetryDelaySeconds),
            CancellationToken.None);

        Assert.False(result);
        var after = await GetOutboxAsync(context.OutboxMesajiId);
        Assert.Equal(before.Durum, after.Durum);
        Assert.Equal(before.KilitToken, after.KilitToken);
        Assert.Equal(before.KilitBitisZamaniUtc, after.KilitBitisZamaniUtc);
        Assert.Equal(before.SonrakiDenemeZamaniUtc, after.SonrakiDenemeZamaniUtc);
        Assert.Equal(before.SonHataKodu, after.SonHataKodu);
        Assert.Equal(before.SonHataMesaji, after.SonHataMesaji);
    }

    [IntegrationFact]
    public async Task SuresiDolmusLeaseFailYapamaz()
    {
        var context = await ClaimAndGetContextAsync();
        await UpdateOutboxAsync(context.OutboxMesajiId, outbox => outbox.KilitBitisZamaniUtc = DateTime.UtcNow.AddMinutes(-5));

        var before = await GetOutboxAsync(context.OutboxMesajiId);
        var transition = CreateTransitionService(SatisBelgesiMuhasebeTestSupport.CreateDbContext());

        var result = await transition.TryFailAsync(
            context.OutboxMesajiId,
            context.KurumId,
            context.KilitToken,
            "E42",
            "Gerçek hata",
            TimeSpan.FromSeconds(RetryDelaySeconds),
            CancellationToken.None);

        Assert.False(result);
        var after = await GetOutboxAsync(context.OutboxMesajiId);
        Assert.Equal(before.KilitBitisZamaniUtc, after.KilitBitisZamaniUtc);
        Assert.Equal(before.Durum, after.Durum);
    }

    [IntegrationFact]
    public async Task AktifLeaseRenewEdilir()
    {
        var context = await ClaimAndGetContextAsync();
        var before = await GetDatabaseUtcAsync();
        var transition = CreateTransitionService(SatisBelgesiMuhasebeTestSupport.CreateDbContext());

        var result = await transition.TryRenewAsync(context.OutboxMesajiId, context.KurumId, context.KilitToken, TimeSpan.FromSeconds(RenewLeaseSeconds), CancellationToken.None);
        var after = await GetDatabaseUtcAsync();

        Assert.True(result);
        var outbox = await GetOutboxAsync(context.OutboxMesajiId);
        Assert.Equal(EBelgeOutboxDurumu.Isleniyor, outbox.Durum);
        Assert.Equal(1, outbox.DenemeSayisi);
        Assert.Equal(context.KilitToken, outbox.KilitToken);
        Assert.True(outbox.KilitBitisZamaniUtc >= before.AddSeconds(RenewLeaseSeconds));
        Assert.True(outbox.KilitBitisZamaniUtc <= after.AddSeconds(RenewLeaseSeconds));
        Assert.Null(outbox.SonrakiDenemeZamaniUtc);
        Assert.Null(outbox.SonHataKodu);
        Assert.Null(outbox.SonHataMesaji);
    }

    [IntegrationFact]
    public async Task YanlisTokenRenewYapamaz()
    {
        var context = await ClaimAndGetContextAsync();
        var before = await GetOutboxAsync(context.OutboxMesajiId);
        var transition = CreateTransitionService(SatisBelgesiMuhasebeTestSupport.CreateDbContext());

        var result = await transition.TryRenewAsync(context.OutboxMesajiId, context.KurumId, Guid.NewGuid().ToString("D"), TimeSpan.FromSeconds(RenewLeaseSeconds), CancellationToken.None);

        Assert.False(result);
        var after = await GetOutboxAsync(context.OutboxMesajiId);
        Assert.Equal(before.KilitBitisZamaniUtc, after.KilitBitisZamaniUtc);
        Assert.Equal(before.KilitToken, after.KilitToken);
        Assert.Equal(before.Durum, after.Durum);
    }

    [IntegrationFact]
    public async Task SuresiDolmusLeaseRenewEdilemez()
    {
        var context = await ClaimAndGetContextAsync();
        await UpdateOutboxAsync(context.OutboxMesajiId, outbox => outbox.KilitBitisZamaniUtc = DateTime.UtcNow.AddMinutes(-5));

        var before = await GetOutboxAsync(context.OutboxMesajiId);
        var transition = CreateTransitionService(SatisBelgesiMuhasebeTestSupport.CreateDbContext());

        var result = await transition.TryRenewAsync(context.OutboxMesajiId, context.KurumId, context.KilitToken, TimeSpan.FromSeconds(RenewLeaseSeconds), CancellationToken.None);

        Assert.False(result);
        var after = await GetOutboxAsync(context.OutboxMesajiId);
        Assert.Equal(before.KilitBitisZamaniUtc, after.KilitBitisZamaniUtc);
        Assert.Equal(before.Durum, after.Durum);
    }

    [IntegrationFact]
    public async Task YanlisKurumIdIleCompleteFailRenewYapilamaz()
    {
        var context = await ClaimAndGetContextAsync();
        var transition = CreateTransitionService(SatisBelgesiMuhasebeTestSupport.CreateDbContext());
        var wrongKurumId = context.KurumId + 999;

        Assert.False(await transition.TryCompleteAsync(context.OutboxMesajiId, wrongKurumId, context.KilitToken, CancellationToken.None));
        Assert.False(await transition.TryFailAsync(context.OutboxMesajiId, wrongKurumId, context.KilitToken, "E42", "Gerçek hata", TimeSpan.FromSeconds(RetryDelaySeconds), CancellationToken.None));
        Assert.False(await transition.TryRenewAsync(context.OutboxMesajiId, wrongKurumId, context.KilitToken, TimeSpan.FromSeconds(RenewLeaseSeconds), CancellationToken.None));

        var after = await GetOutboxAsync(context.OutboxMesajiId);
        Assert.Equal(EBelgeOutboxDurumu.Isleniyor, after.Durum);
        Assert.Equal(context.KilitToken, after.KilitToken);
    }

    [IntegrationFact]
    public async Task EskiTokenIleCompleteFailRenewYapilamazYeniTokenVeLeaseKorunur()
    {
        var completeContext = await ClaimAndGetContextAsync();
        var failContext = await ClaimAndGetContextAsync();
        var renewContext = await ClaimAndGetContextAsync();

        var completeOldToken = completeContext.KilitToken;
        var failOldToken = failContext.KilitToken;
        var renewOldToken = renewContext.KilitToken;

        await UpdateOutboxAsync(completeContext.OutboxMesajiId, outbox => outbox.KilitBitisZamaniUtc = DateTime.UtcNow.AddMinutes(-5));
        var reClaimedComplete = await ClaimOutboxAsync(completeContext.SatisBelgesiId);
        Assert.NotEqual(completeOldToken, reClaimedComplete.KilitToken);
        var completeBefore = await GetOutboxAsync(reClaimedComplete.OutboxMesajiId);
        var transition = CreateTransitionService(SatisBelgesiMuhasebeTestSupport.CreateDbContext());

        Assert.False(await transition.TryCompleteAsync(reClaimedComplete.OutboxMesajiId, reClaimedComplete.KurumId, completeOldToken, CancellationToken.None));

        var completeAfter = await GetOutboxAsync(reClaimedComplete.OutboxMesajiId);

        Assert.Equal(completeBefore.KilitToken, completeAfter.KilitToken);
        Assert.Equal(completeBefore.KilitBitisZamaniUtc, completeAfter.KilitBitisZamaniUtc);

        await UpdateOutboxAsync(failContext.OutboxMesajiId, outbox => outbox.KilitBitisZamaniUtc = DateTime.UtcNow.AddMinutes(-5));
        var reClaimedFail = await ClaimOutboxAsync(failContext.SatisBelgesiId);
        Assert.NotEqual(failOldToken, reClaimedFail.KilitToken);
        var failBefore = await GetOutboxAsync(reClaimedFail.OutboxMesajiId);

        Assert.False(await transition.TryFailAsync(reClaimedFail.OutboxMesajiId, reClaimedFail.KurumId, failOldToken, "E42", "Gerçek hata", TimeSpan.FromSeconds(RetryDelaySeconds), CancellationToken.None));
        var failAfter = await GetOutboxAsync(reClaimedFail.OutboxMesajiId);
        Assert.Equal(failBefore.KilitToken, failAfter.KilitToken);
        Assert.Equal(failBefore.KilitBitisZamaniUtc, failAfter.KilitBitisZamaniUtc);

        await UpdateOutboxAsync(renewContext.OutboxMesajiId, outbox => outbox.KilitBitisZamaniUtc = DateTime.UtcNow.AddMinutes(-5));
        var reClaimedRenew = await ClaimOutboxAsync(renewContext.SatisBelgesiId);
        Assert.NotEqual(renewOldToken, reClaimedRenew.KilitToken);
        var renewBefore = await GetOutboxAsync(reClaimedRenew.OutboxMesajiId);

        Assert.False(await transition.TryRenewAsync(reClaimedRenew.OutboxMesajiId, reClaimedRenew.KurumId, renewOldToken, TimeSpan.FromSeconds(RenewLeaseSeconds), CancellationToken.None));
        var renewAfter = await GetOutboxAsync(reClaimedRenew.OutboxMesajiId);
        Assert.Equal(renewBefore.KilitToken, renewAfter.KilitToken);
        Assert.Equal(renewBefore.KilitBitisZamaniUtc, renewAfter.KilitBitisZamaniUtc);
    }

    [IntegrationFact]
    public async Task SoftDeleteEdilmisKayitUzerindeCompleteFailRenewCalismaz()
    {
        var context = await ClaimAndGetContextAsync();
        await UpdateOutboxAsync(context.OutboxMesajiId, outbox => outbox.IsDeleted = true);

        var baseline = await GetOutboxAsync(context.OutboxMesajiId);
        Assert.True(baseline.IsDeleted);

        var transition = CreateTransitionService(SatisBelgesiMuhasebeTestSupport.CreateDbContext());

        Assert.False(await transition.TryCompleteAsync(context.OutboxMesajiId, context.KurumId, context.KilitToken, CancellationToken.None));
        await AssertOutboxStateUnchangedAsync(context.OutboxMesajiId, baseline);

        Assert.False(await transition.TryFailAsync(context.OutboxMesajiId, context.KurumId, context.KilitToken, "E1", "mesaj", TimeSpan.FromSeconds(RetryDelaySeconds), CancellationToken.None));
        await AssertOutboxStateUnchangedAsync(context.OutboxMesajiId, baseline);

        Assert.False(await transition.TryRenewAsync(context.OutboxMesajiId, context.KurumId, context.KilitToken, TimeSpan.FromSeconds(RenewLeaseSeconds), CancellationToken.None));
        await AssertOutboxStateUnchangedAsync(context.OutboxMesajiId, baseline);
    }

    [IntegrationFact]
    public async Task GecersizCompleteTokenDegerleriReddedilir()
    {
        var context = await ClaimAndGetContextAsync();
        var transition = CreateTransitionService(SatisBelgesiMuhasebeTestSupport.CreateDbContext());

        await Assert.ThrowsAsync<BaseException>(() => transition.TryCompleteAsync(context.OutboxMesajiId, context.KurumId, "", CancellationToken.None));
        await Assert.ThrowsAsync<BaseException>(() => transition.TryCompleteAsync(context.OutboxMesajiId, context.KurumId, "not-a-guid", CancellationToken.None));
    }

    [IntegrationFact]
    public async Task GecersizRetrySureleriReddedilir()
    {
        var context = await ClaimAndGetContextAsync();
        var transition = CreateTransitionService(SatisBelgesiMuhasebeTestSupport.CreateDbContext());
        var overIntMax = TimeSpan.FromTicks(((long)int.MaxValue + 1L) * TimeSpan.TicksPerSecond);

        await Assert.ThrowsAsync<BaseException>(() => transition.TryFailAsync(context.OutboxMesajiId, context.KurumId, context.KilitToken, "E1", "mesaj", TimeSpan.Zero, CancellationToken.None));
        await Assert.ThrowsAsync<BaseException>(() => transition.TryFailAsync(context.OutboxMesajiId, context.KurumId, context.KilitToken, "E1", "mesaj", TimeSpan.FromSeconds(-1), CancellationToken.None));
        await Assert.ThrowsAsync<BaseException>(() => transition.TryFailAsync(context.OutboxMesajiId, context.KurumId, context.KilitToken, "E1", "mesaj", TimeSpan.FromMilliseconds(500), CancellationToken.None));
        await Assert.ThrowsAsync<BaseException>(() => transition.TryFailAsync(context.OutboxMesajiId, context.KurumId, context.KilitToken, "E1", "mesaj", TimeSpan.FromDays(30).Add(TimeSpan.FromSeconds(1)), CancellationToken.None));
        await Assert.ThrowsAsync<BaseException>(() => transition.TryFailAsync(context.OutboxMesajiId, context.KurumId, context.KilitToken, "E1", "mesaj", overIntMax, CancellationToken.None));
        await Assert.ThrowsAsync<BaseException>(() => transition.TryFailAsync(context.OutboxMesajiId, context.KurumId, context.KilitToken, "", "mesaj", null, CancellationToken.None));
        await Assert.ThrowsAsync<BaseException>(() => transition.TryFailAsync(context.OutboxMesajiId, context.KurumId, context.KilitToken, new string('A', 101), "mesaj", null, CancellationToken.None));
        await Assert.ThrowsAsync<BaseException>(() => transition.TryFailAsync(context.OutboxMesajiId, context.KurumId, context.KilitToken, "E1", "", null, CancellationToken.None));
        await Assert.ThrowsAsync<BaseException>(() => transition.TryFailAsync(context.OutboxMesajiId, context.KurumId, context.KilitToken, "E1", new string('B', 2001), null, CancellationToken.None));
    }

    [IntegrationFact]
    public async Task GecersizRenewSureleriReddedilir()
    {
        var context = await ClaimAndGetContextAsync();
        var transition = CreateTransitionService(SatisBelgesiMuhasebeTestSupport.CreateDbContext());
        var overIntMax = TimeSpan.FromTicks(((long)int.MaxValue + 1L) * TimeSpan.TicksPerSecond);

        await Assert.ThrowsAsync<BaseException>(() => transition.TryRenewAsync(context.OutboxMesajiId, context.KurumId, context.KilitToken, TimeSpan.Zero, CancellationToken.None));
        await Assert.ThrowsAsync<BaseException>(() => transition.TryRenewAsync(context.OutboxMesajiId, context.KurumId, context.KilitToken, TimeSpan.FromSeconds(-1), CancellationToken.None));
        await Assert.ThrowsAsync<BaseException>(() => transition.TryRenewAsync(context.OutboxMesajiId, context.KurumId, context.KilitToken, TimeSpan.FromMilliseconds(500), CancellationToken.None));
        await Assert.ThrowsAsync<BaseException>(() => transition.TryRenewAsync(context.OutboxMesajiId, context.KurumId, context.KilitToken, TimeSpan.FromDays(1).Add(TimeSpan.FromSeconds(1)), CancellationToken.None));
        await Assert.ThrowsAsync<BaseException>(() => transition.TryRenewAsync(context.OutboxMesajiId, context.KurumId, context.KilitToken, overIntMax, CancellationToken.None));
    }

    private async Task AssertOutboxStateUnchangedAsync(int outboxMesajiId, EBelgeOutboxMesaji expected)
    {
        var actual = await GetOutboxAsync(outboxMesajiId);
        Assert.Equal(expected.Durum, actual.Durum);
        Assert.Equal(expected.KilitToken, actual.KilitToken);
        Assert.Equal(expected.KilitBitisZamaniUtc, actual.KilitBitisZamaniUtc);
        Assert.Equal(expected.DenemeSayisi, actual.DenemeSayisi);
        Assert.Equal(expected.SonrakiDenemeZamaniUtc, actual.SonrakiDenemeZamaniUtc);
        Assert.Equal(expected.SonHataKodu, actual.SonHataKodu);
        Assert.Equal(expected.SonHataMesaji, actual.SonHataMesaji);
        Assert.Equal(expected.TamamlanmaZamaniUtc, actual.TamamlanmaZamaniUtc);
        Assert.Equal(expected.IsDeleted, actual.IsDeleted);
    }

    // ---- İş-türü-farkında guard (EBelgeKaydiId + IsTuru) (Faz 2B.6.2 görev md.1, Faz 2B.7 görev md.16 - senaryo 6-7) ----

    [IntegrationFact]
    public async Task YanlisEBelgeKaydiIdIleJobIsOwnedCompleteFailYapilamazHicbirAlanDegismez()
    {
        var context = await ClaimAndGetContextAsync();
        var before = await GetOutboxAsync(context.OutboxMesajiId);
        var transition = CreateTransitionService(SatisBelgesiMuhasebeTestSupport.CreateDbContext());
        var wrongEBelgeKaydiId = context.EBelgeKaydiId + 999_000;

        // Doğru token/kurum/outbox/iş türü - ama YANLIŞ EBelgeKaydiId: SQL guard'ı bunu
        // REDDETMELİDİR (bkz. Faz 2B.6.2 görev md.1 - "outbox satırının EBelgeKaydiId alanı
        // kontrol edilmiyor").
        Assert.False(await transition.IsOwnedForJobAsync(context.OutboxMesajiId, context.KurumId, wrongEBelgeKaydiId, EBelgeOutboxIsTuru.ArtefaktOlustur, context.KilitToken, CancellationToken.None));
        Assert.False(await transition.TryCompleteJobAsync(context.OutboxMesajiId, context.KurumId, wrongEBelgeKaydiId, EBelgeOutboxIsTuru.ArtefaktOlustur, context.KilitToken, CancellationToken.None));
        Assert.False(await transition.TryFailJobAsync(context.OutboxMesajiId, context.KurumId, wrongEBelgeKaydiId, EBelgeOutboxIsTuru.ArtefaktOlustur, context.KilitToken, "E1", "mesaj", null, CancellationToken.None));

        var after = await GetOutboxAsync(context.OutboxMesajiId);
        Assert.Equal(before.Durum, after.Durum);
        Assert.Equal(before.KilitToken, after.KilitToken);
        Assert.Equal(before.KilitBitisZamaniUtc, after.KilitBitisZamaniUtc);
        Assert.Equal(before.SonHataKodu, after.SonHataKodu);
        Assert.Equal(before.SonHataMesaji, after.SonHataMesaji);
    }

    [IntegrationFact]
    public async Task DogruEBelgeKaydiIdIleJobIsOwnedTrueDonerVeCompleteBasariliOlur()
    {
        var context = await ClaimAndGetContextAsync();
        var transition = CreateTransitionService(SatisBelgesiMuhasebeTestSupport.CreateDbContext());

        Assert.True(await transition.IsOwnedForJobAsync(context.OutboxMesajiId, context.KurumId, context.EBelgeKaydiId, EBelgeOutboxIsTuru.ArtefaktOlustur, context.KilitToken, CancellationToken.None));

        var completed = await transition.TryCompleteJobAsync(context.OutboxMesajiId, context.KurumId, context.EBelgeKaydiId, EBelgeOutboxIsTuru.ArtefaktOlustur, context.KilitToken, CancellationToken.None);

        Assert.True(completed);
        var outbox = await GetOutboxAsync(context.OutboxMesajiId);
        Assert.Equal(EBelgeOutboxDurumu.Tamamlandi, outbox.Durum);
        Assert.Null(outbox.KilitToken);
    }

    [IntegrationFact]
    public async Task DogruEBelgeKaydiIdIleJobFailTerminalHataUretir()
    {
        var context = await ClaimAndGetContextAsync();
        var transition = CreateTransitionService(SatisBelgesiMuhasebeTestSupport.CreateDbContext());

        var failed = await transition.TryFailJobAsync(
            context.OutboxMesajiId, context.KurumId, context.EBelgeKaydiId, EBelgeOutboxIsTuru.ArtefaktOlustur, context.KilitToken, "EBELGE_TEST", "test hatasi", retryDelay: null, CancellationToken.None);

        Assert.True(failed);
        var outbox = await GetOutboxAsync(context.OutboxMesajiId);
        Assert.Equal(EBelgeOutboxDurumu.Hata, outbox.Durum);
        Assert.Null(outbox.SonrakiDenemeZamaniUtc);
        Assert.Null(outbox.KilitToken);
    }

    [IntegrationFact]
    public async Task YanlisBeklenenIsTuruIleJobGuardTarafindanReddedilirHicbirAlanDegismez()
    {
        // Job-parametreli guard sayesinde (bkz. Faz 2B.7 görev md.16), "yanlış iş türü"
        // senaryosunu üretmek için artık CHECK constraint manipülasyonuna GEREK YOKTUR -
        // GERÇEK bir ArtefaktOlustur satırına, KASITLI OLARAK YANLIŞ bir expectedIsTuru
        // (UblImzala) ile erişilmeye çalışılır ve SQL guard'ı bunu REDDETMELİDİR.
        var context = await ClaimAndGetContextAsync();
        var before = await GetOutboxAsync(context.OutboxMesajiId);
        Assert.Equal(EBelgeOutboxIsTuru.ArtefaktOlustur, before.IsTuru);

        var transition = CreateTransitionService(SatisBelgesiMuhasebeTestSupport.CreateDbContext());

        var sahip = await transition.IsOwnedForJobAsync(context.OutboxMesajiId, context.KurumId, context.EBelgeKaydiId, EBelgeOutboxIsTuru.UblImzala, context.KilitToken, CancellationToken.None);
        var completed = await transition.TryCompleteJobAsync(context.OutboxMesajiId, context.KurumId, context.EBelgeKaydiId, EBelgeOutboxIsTuru.UblImzala, context.KilitToken, CancellationToken.None);
        var failed = await transition.TryFailJobAsync(context.OutboxMesajiId, context.KurumId, context.EBelgeKaydiId, EBelgeOutboxIsTuru.UblImzala, context.KilitToken, "E1", "mesaj", null, CancellationToken.None);

        Assert.False(sahip);
        Assert.False(completed);
        Assert.False(failed);

        var after = await GetOutboxAsync(context.OutboxMesajiId);
        Assert.Equal(before.Durum, after.Durum);
        Assert.Equal(before.KilitToken, after.KilitToken);
    }

    private sealed record ClaimedOutboxContext(
        int SatisBelgesiId,
        int OutboxMesajiId,
        int KurumId,
        int EBelgeKaydiId,
        string KilitToken,
        DateTime? KilitBitisZamaniUtc,
        int DenemeSayisi);
}
