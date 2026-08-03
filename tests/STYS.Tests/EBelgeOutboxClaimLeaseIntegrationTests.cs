using AutoMapper;
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
public class EBelgeOutboxClaimLeaseIntegrationTests : IAsyncLifetime
{
    private const string TestMarker = "EBO-2A2";
    private const int LeaseSeconds = 30;

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

    private static StysAppDbContext CreateDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<StysAppDbContext>()
            .UseSqlServer(SatisBelgesiMuhasebeTestSupport.ConnectionString);

        return new StysAppDbContext(
            optionsBuilder.Options,
            new SatisBelgesiMuhasebeTestSupport.FakeCurrentUserAccessor(),
            new TestTenantAccessor());
    }

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

    private static string TruncateToMax(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];

    private async Task EnsureMuhasebeFisIdAsync(int satisBelgesiId)
    {
        await using var verifyCtx = CreateDbContext();
        var muhasebeFisId = await verifyCtx.SatisBelgeleri
            .AsNoTracking()
            .Where(x => x.Id == satisBelgesiId)
            .Select(x => x.MuhasebeFisId)
            .SingleAsync();

        Assert.True(muhasebeFisId.HasValue);
    }

    private async Task<SatisBelgesiDto> CreateAndCutOutgoingInvoiceAsync()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var created = await service.CreateAsync(BuildSatisBelgesiRequest());
        await service.MuhasebeOnayinaGonderAsync(created.Id!.Value, CancellationToken.None);
        await service.MuhasebeOnaylaAsync(created.Id.Value, CancellationToken.None);

        var donemService = SatisBelgesiMuhasebeTestSupport.CreateRealMuhasebeDonemService(dbContext);
        var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext, donemService);
        await fisService.MuhasebeFisiOlusturAsync(created.Id.Value, CancellationToken.None);
        await EnsureMuhasebeFisIdAsync(created.Id.Value);

        await using var kesimCtx = CreateDbContext();
        var kesimService = CreateService(kesimCtx);
        return await kesimService.FaturaKesAsync(created.Id.Value, new FaturaKesRequest { SeriKodu = "EBF" }, CancellationToken.None);
    }

    private static async Task<EBelgeOutboxMesaji> GetOutboxAsync(int satisBelgesiId)
    {
        await using var verifyCtx = CreateDbContext();
        return await verifyCtx.EBelgeOutboxMesajlari
            .IgnoreQueryFilters()
            .SingleAsync(x => x.EBelgeKaydi.SatisBelgesiId == satisBelgesiId);
    }

    private static async Task<int> GetOutboxMesajiIdAsync(int satisBelgesiId)
    {
        await using var verifyCtx = CreateDbContext();
        return await verifyCtx.EBelgeOutboxMesajlari
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.EBelgeKaydi.SatisBelgesiId == satisBelgesiId)
            .Select(x => x.Id)
            .SingleAsync();
    }

    private static async Task<int> GetEBelgeKaydiIdAsync(int satisBelgesiId)
    {
        await using var verifyCtx = CreateDbContext();
        return await verifyCtx.EBelgeKayitlari
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.SatisBelgesiId == satisBelgesiId)
            .Select(x => x.Id)
            .SingleAsync();
    }

    private static async Task<EBelgeOutboxClaimLeaseResultDto?> ClaimNextAsync()
    {
        await using var claimCtx = CreateDbContext();
        var claimService = CreateClaimService(claimCtx);
        return await claimService.TryClaimNextAsync(TimeSpan.FromSeconds(LeaseSeconds), CancellationToken.None);
    }

    private static async Task UpdateOutboxAsync(int satisBelgesiId, Action<EBelgeOutboxMesaji> mutator)
    {
        await using var updateCtx = CreateDbContext();
        var outbox = await updateCtx.EBelgeOutboxMesajlari
            .IgnoreQueryFilters()
            .SingleAsync(x => x.EBelgeKaydi.SatisBelgesiId == satisBelgesiId);

        mutator(outbox);
        await updateCtx.SaveChangesAsync();
    }

    [IntegrationFact]
    public async Task TekBekleyenMesajEszamanliIkiClaimDenemesindeYalnizBiriKullanilir()
    {
        var cut = await CreateAndCutOutgoingInvoiceAsync();
        var eBelgeKaydiId = await GetEBelgeKaydiIdAsync(cut.Id!.Value);

        var ready = new CountdownEvent(2);
        var start = new ManualResetEventSlim(false);

        Task<EBelgeOutboxClaimLeaseResultDto?> ClaimInParallelAsync() => Task.Run(async () =>
        {
            ready.Signal();
            start.Wait();
            return await ClaimNextAsync();
        });

        var first = ClaimInParallelAsync();
        var second = ClaimInParallelAsync();

        Assert.True(ready.Wait(TimeSpan.FromSeconds(10)), "Iki claim gorevi baslamaya hazir hale gelemedi.");
        start.Set();

        await Task.WhenAll(first, second);

        var results = new[] { await first, await second };
        Assert.Equal(1, results.Count(x => x is not null));

        var claimed = results.Single(x => x is not null)!;
        Assert.Equal(eBelgeKaydiId, claimed.EBelgeKaydiId);
        Assert.Equal(_kurumId, claimed.KurumId);
    }

    [IntegrationFact]
    public async Task BasariliClaimDurumuIsleniyorVeLeaseAlanlariniDoldurur()
    {
        var cut = await CreateAndCutOutgoingInvoiceAsync();
        var eBelgeKaydiId = await GetEBelgeKaydiIdAsync(cut.Id!.Value);

        var claimed = await ClaimNextAsync();
        Assert.NotNull(claimed);
        Assert.Equal(eBelgeKaydiId, claimed!.EBelgeKaydiId);
        Assert.Equal(_kurumId, claimed.KurumId);
        Assert.Equal(EBelgeOutboxIsTuru.ArtefaktOlustur, claimed.IsTuru);
        Assert.Equal(EBelgeOutboxDurumu.Isleniyor, claimed.Durum);
        Assert.Equal(1, claimed.DenemeSayisi);
        Assert.False(string.IsNullOrWhiteSpace(claimed.KilitToken));
        Assert.True(Guid.TryParse(claimed.KilitToken, out _));
        Assert.NotEqual(default(DateTime), claimed.IslemBaslamaZamaniUtc);
        Assert.True(claimed.KilitBitisZamaniUtc > claimed.IslemBaslamaZamaniUtc);
        Assert.InRange((claimed.KilitBitisZamaniUtc - claimed.IslemBaslamaZamaniUtc).TotalSeconds, 29, 31);
        Assert.Null(claimed.SonrakiDenemeZamaniUtc);

        await using var verifyCtx = CreateDbContext();
        var outbox = await verifyCtx.EBelgeOutboxMesajlari
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(x => x.EBelgeKaydi.SatisBelgesiId == cut.Id.Value);

        Assert.Equal(EBelgeOutboxDurumu.Isleniyor, outbox.Durum);
        Assert.Equal(claimed.KilitToken, outbox.KilitToken);
        Assert.Equal(claimed.KilitBitisZamaniUtc, outbox.KilitBitisZamaniUtc);
        Assert.Equal(claimed.IslemBaslamaZamaniUtc, outbox.IslemBaslamaZamaniUtc);
        Assert.Equal(1, outbox.DenemeSayisi);
        Assert.Null(outbox.SonrakiDenemeZamaniUtc);
    }

    [IntegrationFact]
    public async Task AktifLeaseTasiyanIsleniyorKaydiTekrarClaimEdilmez()
    {
        var cut = await CreateAndCutOutgoingInvoiceAsync();

        await UpdateOutboxAsync(cut.Id!.Value, outbox =>
        {
            outbox.Durum = EBelgeOutboxDurumu.Isleniyor;
            outbox.DenemeSayisi = 2;
            outbox.KilitToken = Guid.NewGuid().ToString("D");
            outbox.IslemBaslamaZamaniUtc = DateTime.UtcNow.AddMinutes(-1);
            outbox.KilitBitisZamaniUtc = DateTime.UtcNow.AddMinutes(10);
            outbox.SonrakiDenemeZamaniUtc = null;
        });

        var claimed = await ClaimNextAsync();
        Assert.Null(claimed);

        var outbox = await GetOutboxAsync(cut.Id.Value);
        Assert.Equal(EBelgeOutboxDurumu.Isleniyor, outbox.Durum);
        Assert.Equal(2, outbox.DenemeSayisi);
        Assert.False(string.IsNullOrWhiteSpace(outbox.KilitToken));
        Assert.True(outbox.KilitBitisZamaniUtc.HasValue);
    }

    [IntegrationFact]
    public async Task UygunlukZamaniSiralamasiDurumdanBagimsizCalisir()
    {
        var bekliyorCut = await CreateAndCutOutgoingInvoiceAsync();
        var hataCut = await CreateAndCutOutgoingInvoiceAsync();
        var isleniyorCut = await CreateAndCutOutgoingInvoiceAsync();

        var bekliyorOutboxId = await GetOutboxMesajiIdAsync(bekliyorCut.Id!.Value);
        var hataOutboxId = await GetOutboxMesajiIdAsync(hataCut.Id!.Value);
        var isleniyorOutboxId = await GetOutboxMesajiIdAsync(isleniyorCut.Id!.Value);

        var now = DateTime.UtcNow;
        var isleniyorZamani = now.AddMinutes(-30);
        var hataZamani = now.AddMinutes(-20);
        var bekliyorZamani = now.AddMinutes(-10);

        await UpdateOutboxAsync(isleniyorCut.Id.Value, outbox =>
        {
            outbox.Durum = EBelgeOutboxDurumu.Isleniyor;
            outbox.DenemeSayisi = 2;
            outbox.KilitToken = Guid.NewGuid().ToString("D");
            outbox.IslemBaslamaZamaniUtc = now.AddMinutes(-45);
            outbox.KilitBitisZamaniUtc = isleniyorZamani;
            outbox.SonrakiDenemeZamaniUtc = null;
        });

        await UpdateOutboxAsync(hataCut.Id.Value, outbox =>
        {
            outbox.Durum = EBelgeOutboxDurumu.Hata;
            outbox.DenemeSayisi = 3;
            outbox.SonrakiDenemeZamaniUtc = hataZamani;
            outbox.KilitToken = null;
            outbox.KilitBitisZamaniUtc = null;
            outbox.IslemBaslamaZamaniUtc = null;
        });

        await UpdateOutboxAsync(bekliyorCut.Id.Value, outbox =>
        {
            outbox.Durum = EBelgeOutboxDurumu.Bekliyor;
            outbox.DenemeSayisi = 0;
            outbox.SonrakiDenemeZamaniUtc = bekliyorZamani;
            outbox.KilitToken = null;
            outbox.KilitBitisZamaniUtc = null;
            outbox.IslemBaslamaZamaniUtc = null;
        });

        var first = await ClaimNextAsync();
        var second = await ClaimNextAsync();
        var third = await ClaimNextAsync();

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotNull(third);

        Assert.Equal(isleniyorOutboxId, first!.OutboxMesajiId);
        Assert.Equal(hataOutboxId, second!.OutboxMesajiId);
        Assert.Equal(bekliyorOutboxId, third!.OutboxMesajiId);

        var firstOutbox = await GetOutboxAsync(isleniyorCut.Id.Value);
        var secondOutbox = await GetOutboxAsync(hataCut.Id.Value);
        var thirdOutbox = await GetOutboxAsync(bekliyorCut.Id.Value);

        Assert.Equal(EBelgeOutboxDurumu.Isleniyor, firstOutbox.Durum);
        Assert.Equal(EBelgeOutboxDurumu.Isleniyor, secondOutbox.Durum);
        Assert.Equal(EBelgeOutboxDurumu.Isleniyor, thirdOutbox.Durum);
    }

    [IntegrationFact]
    public async Task EsitUygunlukZamanindaKucukIdOnceClaimEdilir()
    {
        var ilkCut = await CreateAndCutOutgoingInvoiceAsync();
        var ikinciCut = await CreateAndCutOutgoingInvoiceAsync();

        var ilkOutboxId = await GetOutboxMesajiIdAsync(ilkCut.Id!.Value);
        var ikinciOutboxId = await GetOutboxMesajiIdAsync(ikinciCut.Id!.Value);

        var esitZaman = DateTime.UtcNow.AddMinutes(-25);

        await UpdateOutboxAsync(ilkCut.Id.Value, outbox =>
        {
            outbox.Durum = EBelgeOutboxDurumu.Bekliyor;
            outbox.DenemeSayisi = 0;
            outbox.SonrakiDenemeZamaniUtc = esitZaman;
            outbox.KilitToken = null;
            outbox.KilitBitisZamaniUtc = null;
            outbox.IslemBaslamaZamaniUtc = null;
        });

        await UpdateOutboxAsync(ikinciCut.Id.Value, outbox =>
        {
            outbox.Durum = EBelgeOutboxDurumu.Hata;
            outbox.DenemeSayisi = 4;
            outbox.SonrakiDenemeZamaniUtc = esitZaman;
            outbox.KilitToken = null;
            outbox.KilitBitisZamaniUtc = null;
            outbox.IslemBaslamaZamaniUtc = null;
        });

        var first = await ClaimNextAsync();
        var second = await ClaimNextAsync();

        Assert.NotNull(first);
        Assert.NotNull(second);

        var beklenenIlkId = Math.Min(ilkOutboxId, ikinciOutboxId);
        var beklenenIkinciId = Math.Max(ilkOutboxId, ikinciOutboxId);

        Assert.Equal(beklenenIlkId, first!.OutboxMesajiId);
        Assert.Equal(beklenenIkinciId, second!.OutboxMesajiId);
    }

    [IntegrationFact]
    public async Task LeaseSuresiDolmusIsleniyorKaydiYenidenClaimEdilir()
    {
        var cut = await CreateAndCutOutgoingInvoiceAsync();

        await UpdateOutboxAsync(cut.Id!.Value, outbox =>
        {
            outbox.Durum = EBelgeOutboxDurumu.Isleniyor;
            outbox.DenemeSayisi = 4;
            outbox.KilitToken = Guid.NewGuid().ToString("D");
            outbox.IslemBaslamaZamaniUtc = DateTime.UtcNow.AddMinutes(-5);
            outbox.KilitBitisZamaniUtc = DateTime.UtcNow.AddMinutes(-1);
            outbox.SonrakiDenemeZamaniUtc = null;
        });

        var claimed = await ClaimNextAsync();
        Assert.NotNull(claimed);
        Assert.Equal(5, claimed!.DenemeSayisi);
        Assert.Equal(EBelgeOutboxDurumu.Isleniyor, claimed.Durum);

        var outbox = await GetOutboxAsync(cut.Id.Value);
        Assert.Equal(EBelgeOutboxDurumu.Isleniyor, outbox.Durum);
        Assert.Equal(5, outbox.DenemeSayisi);
        Assert.Equal(claimed.KilitToken, outbox.KilitToken);
        Assert.Equal(claimed.KilitBitisZamaniUtc, outbox.KilitBitisZamaniUtc);
        Assert.Equal(claimed.IslemBaslamaZamaniUtc, outbox.IslemBaslamaZamaniUtc);
        Assert.True(outbox.KilitBitisZamaniUtc > outbox.IslemBaslamaZamaniUtc);
    }

    [IntegrationFact]
    public async Task GelecekRetryTarihliHataKaydiClaimEdilmez()
    {
        var cut = await CreateAndCutOutgoingInvoiceAsync();

        await UpdateOutboxAsync(cut.Id!.Value, outbox =>
        {
            outbox.Durum = EBelgeOutboxDurumu.Hata;
            outbox.DenemeSayisi = 3;
            outbox.SonrakiDenemeZamaniUtc = DateTime.UtcNow.AddMinutes(10);
            outbox.KilitBitisZamaniUtc = null;
            outbox.KilitToken = null;
            outbox.IslemBaslamaZamaniUtc = null;
        });

        var claimed = await ClaimNextAsync();
        Assert.Null(claimed);

        var outbox = await GetOutboxAsync(cut.Id.Value);
        Assert.Equal(EBelgeOutboxDurumu.Hata, outbox.Durum);
        Assert.Equal(3, outbox.DenemeSayisi);
        Assert.NotNull(outbox.SonrakiDenemeZamaniUtc);
    }

    [IntegrationFact]
    public async Task RetryTarihiGelmisseHataKaydiClaimEdilir()
    {
        var cut = await CreateAndCutOutgoingInvoiceAsync();

        await UpdateOutboxAsync(cut.Id!.Value, outbox =>
        {
            outbox.Durum = EBelgeOutboxDurumu.Hata;
            outbox.DenemeSayisi = 7;
            outbox.SonrakiDenemeZamaniUtc = DateTime.UtcNow.AddMinutes(-10);
            outbox.KilitBitisZamaniUtc = null;
            outbox.KilitToken = null;
            outbox.IslemBaslamaZamaniUtc = null;
        });

        var claimed = await ClaimNextAsync();
        Assert.NotNull(claimed);
        Assert.Equal(8, claimed!.DenemeSayisi);
        Assert.Equal(EBelgeOutboxDurumu.Isleniyor, claimed.Durum);
        Assert.Null(claimed.SonrakiDenemeZamaniUtc);

        var outbox = await GetOutboxAsync(cut.Id.Value);
        Assert.Equal(EBelgeOutboxDurumu.Isleniyor, outbox.Durum);
        Assert.Equal(8, outbox.DenemeSayisi);
        Assert.False(string.IsNullOrWhiteSpace(outbox.KilitToken));
        Assert.Null(outbox.SonrakiDenemeZamaniUtc);
    }

    [IntegrationFact]
    public async Task RetryTarihiBosHataKaydiClaimEdilmez()
    {
        var cut = await CreateAndCutOutgoingInvoiceAsync();

        await UpdateOutboxAsync(cut.Id!.Value, outbox =>
        {
            outbox.Durum = EBelgeOutboxDurumu.Hata;
            outbox.DenemeSayisi = 9;
            outbox.SonrakiDenemeZamaniUtc = null;
            outbox.KilitBitisZamaniUtc = null;
            outbox.KilitToken = null;
            outbox.IslemBaslamaZamaniUtc = null;
        });

        var claimed = await ClaimNextAsync();
        Assert.Null(claimed);

        var outbox = await GetOutboxAsync(cut.Id.Value);
        Assert.Equal(EBelgeOutboxDurumu.Hata, outbox.Durum);
        Assert.Equal(9, outbox.DenemeSayisi);
        Assert.Null(outbox.SonrakiDenemeZamaniUtc);
    }

    [IntegrationFact]
    public async Task TamamlandiVeSoftDeleteEdilmisKayitlarClaimEdilmez()
    {
        var tamamlandiCut = await CreateAndCutOutgoingInvoiceAsync();
        await UpdateOutboxAsync(tamamlandiCut.Id!.Value, outbox =>
        {
            outbox.Durum = EBelgeOutboxDurumu.Tamamlandi;
            outbox.DenemeSayisi = 1;
            outbox.TamamlanmaZamaniUtc = DateTime.UtcNow.AddMinutes(-1);
            outbox.KilitToken = null;
            outbox.KilitBitisZamaniUtc = null;
            outbox.IslemBaslamaZamaniUtc = null;
            outbox.SonrakiDenemeZamaniUtc = null;
        });

        var tamamlandiClaim = await ClaimNextAsync();
        Assert.Null(tamamlandiClaim);

        var tamamlandiOutbox = await GetOutboxAsync(tamamlandiCut.Id.Value);
        Assert.Equal(EBelgeOutboxDurumu.Tamamlandi, tamamlandiOutbox.Durum);
        Assert.Equal(1, tamamlandiOutbox.DenemeSayisi);

        var softDeleteCut = await CreateAndCutOutgoingInvoiceAsync();
        await UpdateOutboxAsync(softDeleteCut.Id!.Value, outbox =>
        {
            outbox.Durum = EBelgeOutboxDurumu.Bekliyor;
            outbox.DenemeSayisi = 0;
            outbox.KilitToken = null;
            outbox.KilitBitisZamaniUtc = null;
            outbox.IslemBaslamaZamaniUtc = null;
            outbox.SonrakiDenemeZamaniUtc = null;
        });

        await using (var deleteCtx = CreateDbContext())
        {
            var outbox = await deleteCtx.EBelgeOutboxMesajlari
                .IgnoreQueryFilters()
                .SingleAsync(x => x.EBelgeKaydi.SatisBelgesiId == softDeleteCut.Id!.Value);

            deleteCtx.Remove(outbox);
            await deleteCtx.SaveChangesAsync();
        }

        var softDeleteClaim = await ClaimNextAsync();
        Assert.Null(softDeleteClaim);

        var softDeleteOutbox = await GetOutboxAsync(softDeleteCut.Id.Value);
        Assert.True(softDeleteOutbox.IsDeleted);
        Assert.Equal(EBelgeOutboxDurumu.Bekliyor, softDeleteOutbox.Durum);
    }

    private sealed class TestTenantAccessor : TOD.Platform.Security.Auth.Services.ICurrentTenantAccessor
    {
        public int? GetCurrentKurumId() => null;
        public IReadOnlyList<int> GetAccessibleKurumIds() => [];
        public bool IsSuperAdmin() => true;
        public bool IsKurumAdmin() => false;
    }
}
