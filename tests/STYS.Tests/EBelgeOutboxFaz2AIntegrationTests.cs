using System.Data.Common;
using AutoMapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;
using Xunit;

namespace STYS.Tests;

[Trait("Category", "Integration")]
[Collection(SqlServerIntegrationCollection.Name)]
public class EBelgeOutboxFaz2AIntegrationTests : IAsyncLifetime
{
    private const string TestMarker = "EBO-2A";

    private string _uniqueSuffix = TestMarker;
    private DateTime _classStartUtc;
    private int _kurumId;
    private int _ilId;
    private int _tesisId;
    private int _musteriKartId;
    private int _tedarikciKartId;

    public async Task InitializeAsync()
    {
        _classStartUtc = DateTime.UtcNow;
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

    private static StysAppDbContext CreateDbContext(
        IInterceptor? interceptor = null,
        int? currentKurumId = null,
        bool isSuperAdmin = true)
    {
        var optionsBuilder = new DbContextOptionsBuilder<StysAppDbContext>()
            .UseSqlServer(SatisBelgesiMuhasebeTestSupport.ConnectionString);

        if (interceptor is not null)
        {
            optionsBuilder.AddInterceptors(interceptor);
        }

        return new StysAppDbContext(
            optionsBuilder.Options,
            new SatisBelgesiMuhasebeTestSupport.FakeCurrentUserAccessor(),
            new TestTenantAccessor(currentKurumId, isSuperAdmin));
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

    [IntegrationFact]
    public async Task BasariliKesimdeTekBekleyenArtefaktOutboxMesajiOlusur()
    {
        var cut = await CreateAndCutOutgoingInvoiceAsync();

        await using var verifyCtx = CreateDbContext();
        var belge = await verifyCtx.SatisBelgeleri
            .AsNoTracking()
            .Include(x => x.EBelgeKaydi)
            .SingleAsync(x => x.Id == cut.Id.Value);

        var outboxMesaji = await verifyCtx.EBelgeOutboxMesajlari
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(x => x.EBelgeKaydiId == belge.EBelgeKaydi!.Id);

        Assert.Equal(_kurumId, outboxMesaji.KurumId);
        Assert.Equal(belge.EBelgeKaydi.Id, outboxMesaji.EBelgeKaydiId);
        Assert.Equal(EBelgeOutboxIsTuru.ArtefaktOlustur, outboxMesaji.IsTuru);
        Assert.Equal(EBelgeOutboxDurumu.Bekliyor, outboxMesaji.Durum);
        Assert.Equal(0, outboxMesaji.DenemeSayisi);
        Assert.Null(outboxMesaji.SonrakiDenemeZamaniUtc);
        Assert.Null(outboxMesaji.KilitToken);
        Assert.Null(outboxMesaji.KilitBitisZamaniUtc);
        Assert.Null(outboxMesaji.IslemBaslamaZamaniUtc);
        Assert.Null(outboxMesaji.TamamlanmaZamaniUtc);
        Assert.Null(outboxMesaji.SonHataKodu);
        Assert.Null(outboxMesaji.SonHataMesaji);
    }

    [IntegrationFact]
    public async Task AyniBelgeTekrarKesildigindeOutboxSayisiBirKalir()
    {
        var cut = await CreateAndCutOutgoingInvoiceAsync();

        await using var kesimCtx = CreateDbContext();
        var kesimService = CreateService(kesimCtx);
        var ikinci = await kesimService.FaturaKesAsync(cut.Id!.Value, new FaturaKesRequest { SeriKodu = "EBF" }, CancellationToken.None);

        Assert.Equal(cut.ResmiFaturaNo, ikinci.ResmiFaturaNo);
        Assert.Equal(cut.EBelgeUuid, ikinci.EBelgeUuid);

        await using var verifyCtx = CreateDbContext();
        var outboxSayisi = await verifyCtx.EBelgeOutboxMesajlari
            .IgnoreQueryFilters()
            .CountAsync(x => x.EBelgeKaydi.SatisBelgesiId == cut.Id.Value);

        Assert.Equal(1, outboxSayisi);
    }

    [IntegrationFact]
    public async Task KanalBelirlenemezseRollbackOlurVeOutboxOlusmaz()
    {
        await using var seedCtx = CreateDbContext();
        var service = CreateService(seedCtx);
        var created = await service.CreateAsync(BuildSatisBelgesiRequest());
        await service.MuhasebeOnayinaGonderAsync(created.Id!.Value, CancellationToken.None);
        await service.MuhasebeOnaylaAsync(created.Id.Value, CancellationToken.None);

        var donemService = SatisBelgesiMuhasebeTestSupport.CreateRealMuhasebeDonemService(seedCtx);
        var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(seedCtx, donemService);
        await fisService.MuhasebeFisiOlusturAsync(created.Id.Value, CancellationToken.None);
        await EnsureMuhasebeFisIdAsync(created.Id.Value);

        var sayacOnce = await seedCtx.KurumFaturaNumaraSayaclari
            .AsNoTracking()
            .SingleAsync(x => x.KurumId == _kurumId && x.MaliYil == 2026 && x.SeriKodu == "EBF");

        await using (var mutateCtx = CreateDbContext())
        {
            var cariKart = await mutateCtx.CariKartlar.SingleAsync(x => x.Id == _musteriKartId);
            cariKart.EFaturaMukellefiMi = false;
            cariKart.EArsivKapsamindaMi = false;
            await mutateCtx.SaveChangesAsync();
        }

        await using var kesimCtx = CreateDbContext();
        var kesimService = CreateService(kesimCtx);
        var ex = await Assert.ThrowsAsync<BaseException>(
            () => kesimService.FaturaKesAsync(created.Id.Value, new FaturaKesRequest { SeriKodu = "EBF" }, CancellationToken.None));

        Assert.Contains("e-Fatura ya da e-Arşiv", ex.Message, StringComparison.OrdinalIgnoreCase);

        await using var verifyCtx = CreateDbContext();
        var belge = await verifyCtx.SatisBelgeleri.AsNoTracking().SingleAsync(x => x.Id == created.Id.Value);
        Assert.Null(belge.ResmiFaturaNo);
        Assert.Null(belge.EBelgeKaydi);
        Assert.False(await verifyCtx.EBelgeSnapshots.IgnoreQueryFilters().AnyAsync(x => x.EBelgeKaydi.SatisBelgesiId == created.Id.Value));
        Assert.False(await verifyCtx.EBelgeOutboxMesajlari.IgnoreQueryFilters().AnyAsync(x => x.EBelgeKaydi.SatisBelgesiId == created.Id.Value));

        var sayacSonra = await verifyCtx.KurumFaturaNumaraSayaclari
            .AsNoTracking()
            .SingleAsync(x => x.KurumId == _kurumId && x.MaliYil == 2026 && x.SeriKodu == "EBF");

        Assert.Equal(sayacOnce.SonNumara, sayacSonra.SonNumara);
    }

    [IntegrationFact]
    public async Task OutboxSoftDeleteEdilseBileAyniBelgeVeIsTuruTekrarKullanilamaz()
    {
        var cut = await CreateAndCutOutgoingInvoiceAsync();

        await using (var softDeleteCtx = CreateDbContext())
        {
            var mesaj = await softDeleteCtx.EBelgeOutboxMesajlari
                .IgnoreQueryFilters()
                .SingleAsync(x => x.EBelgeKaydi.SatisBelgesiId == cut.Id.Value);

            softDeleteCtx.Remove(mesaj);
            await softDeleteCtx.SaveChangesAsync();
            Assert.True(mesaj.IsDeleted);
        }

        await using (var insertCtx = CreateDbContext())
        {
            var eBelgeKaydiId = await insertCtx.SatisBelgeleri
                .AsNoTracking()
                .Where(x => x.Id == cut.Id.Value)
                .Select(x => x.EBelgeKaydi!.Id)
                .SingleAsync();

            insertCtx.EBelgeOutboxMesajlari.Add(new EBelgeOutboxMesaji
            {
                KurumId = _kurumId,
                EBelgeKaydiId = eBelgeKaydiId,
                IsTuru = EBelgeOutboxIsTuru.ArtefaktOlustur,
                Durum = EBelgeOutboxDurumu.Bekliyor,
                DenemeSayisi = 0
            });

            await Assert.ThrowsAsync<DbUpdateException>(() => insertCtx.SaveChangesAsync());
        }
    }

    [IntegrationFact]
    public async Task CrossoverTenantOutboxBaglantisiDbTarafindanReddedilir()
    {
        var cut = await CreateAndCutOutgoingInvoiceAsync();

        await using var verifyCtx = CreateDbContext();
        var belge = await verifyCtx.SatisBelgeleri
            .AsNoTracking()
            .Include(x => x.EBelgeKaydi)
            .SingleAsync(x => x.Id == cut.Id.Value);

        await using var invalidCtx = CreateDbContext();
        invalidCtx.EBelgeOutboxMesajlari.Add(new EBelgeOutboxMesaji
        {
            KurumId = _kurumId + 999,
            EBelgeKaydiId = belge.EBelgeKaydi!.Id,
            IsTuru = EBelgeOutboxIsTuru.ArtefaktOlustur,
            Durum = EBelgeOutboxDurumu.Bekliyor,
            DenemeSayisi = 0
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => invalidCtx.SaveChangesAsync());
    }

    [IntegrationFact]
    public async Task OutboxIndexUniqueVeFiltresizOlmali()
    {
        var connectionString = SatisBelgesiMuhasebeTestSupport.ConnectionString
            ?? throw new InvalidOperationException("Connection string bulunamadi.");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
SELECT is_unique, filter_definition
FROM sys.indexes
WHERE name = N'IX_EBelgeOutboxMesajlari_EBelgeKaydiId_IsTuru'
  AND object_id = OBJECT_ID(N'[muhasebe].[EBelgeOutboxMesajlari]')
""";

        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.True(reader.GetBoolean(0));
        Assert.True(reader.IsDBNull(1));
    }

    [IntegrationFact]
    public async Task MigrationBackfillAktifKayitIcinBirMesajUretir()
    {
        await using var verifyCtx = CreateDbContext();
        var kayit = await verifyCtx.EBelgeKayitlari
            .AsNoTracking()
            .Include(x => x.SatisBelgesi)
            .Where(x => !x.IsDeleted
                        && x.CreatedAt.HasValue
                        && x.CreatedAt.Value < _classStartUtc
                        && x.SatisBelgesi.BelgeNo != null
                        && !x.SatisBelgesi.BelgeNo.Contains(_uniqueSuffix))
            .OrderBy(x => x.Id)
            .FirstAsync();

        var outboxMesaji = await verifyCtx.EBelgeOutboxMesajlari
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(x => x.EBelgeKaydiId == kayit.Id && x.IsTuru == EBelgeOutboxIsTuru.ArtefaktOlustur);

        Assert.Equal(kayit.KurumId, outboxMesaji.KurumId);
        Assert.Equal(EBelgeOutboxDurumu.Bekliyor, outboxMesaji.Durum);
    }

    private sealed class TestTenantAccessor : ICurrentTenantAccessor
    {
        private readonly int? _currentKurumId;
        private readonly bool _isSuperAdmin;

        public TestTenantAccessor(int? currentKurumId, bool isSuperAdmin)
        {
            _currentKurumId = currentKurumId;
            _isSuperAdmin = isSuperAdmin;
        }

        public int? GetCurrentKurumId() => _currentKurumId;
        public IReadOnlyList<int> GetAccessibleKurumIds() => _currentKurumId.HasValue ? [_currentKurumId.Value] : [];
        public bool IsSuperAdmin() => _isSuperAdmin;
        public bool IsKurumAdmin() => false;
    }
}
