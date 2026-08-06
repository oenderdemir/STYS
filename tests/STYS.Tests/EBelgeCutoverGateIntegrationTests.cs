using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Kurumlar.Entities;
using STYS.Muhasebe.CariKartlar.Mapping;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.MuhasebeDonemleri.Mapping;
using STYS.Muhasebe.SatisBelgeleri;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Muhasebe.SatisBelgeleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using STYS.Muhasebe.SatisBelgeleri.Mapping;
using STYS.Muhasebe.SatisBelgeleri.Repositories;
using STYS.Muhasebe.SatisBelgeleri.Services;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// Faz 2B.4.1: 14.09.2026 Türkiye yerel tarih kapısını, gerçek SQL Server'a karşı, gerçek
/// FaturaKesAsync akışıyla doğrular. Kesim anı, gerçek sistem saati DEĞİL, kontrollü bir
/// FakeTimeProvider'dan gelir (bkz. docs/e-belge-ubl-pdf-eposta-renderer-hazirlik-raporu.md
/// §9 "Kesim anı sözleşmesi").
/// </summary>
[Trait("Category", "Integration")]
[Collection(SqlServerIntegrationCollection.Name)]
[Trait("Domain", "EBelge")]
[Trait("TestLevel", "SqlIntegration")]
[Trait("Dependency", "SqlServer")]
public class EBelgeCutoverGateIntegrationTests : IAsyncLifetime
{
    private const string TestMarker = "EBF-CUT";
    private const string SeriKodu = "ECG";

    private string _uniqueSuffix = TestMarker;
    private int _kurumId;
    private int _ilId;
    private int _tesisId;
    private int _musteriKartId;

    public async Task InitializeAsync()
    {
        _uniqueSuffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];

        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var (kurum, il, tesis) = await SatisBelgesiMuhasebeTestSupport.SeedKurumIlTesisAsync(dbContext, _uniqueSuffix);
        _kurumId = kurum.Id;
        _ilId = il.Id;
        _tesisId = tesis.Id;

        // Faz 2B.4.2 kesim öncesi UBL kapısı, satıcı için yapısal adres (ilçe/il) ister.
        kurum.Ilce = "Kadıköy";
        kurum.Il = "İstanbul";
        await dbContext.SaveChangesAsync();

        var musteriHesap = SatisBelgesiMuhasebeTestSupport.BuildHesap(_uniqueSuffix, "MUS", _tesisId);
        var gelirHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(_uniqueSuffix, MuhasebeAnaHesapKodlari.GelirSatis, "GELIR", _tesisId);
        var kdvHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(_uniqueSuffix, MuhasebeAnaHesapKodlari.KDVHesaplanan, "KDV", _tesisId);
        dbContext.MuhasebeHesapPlanlari.AddRange(musteriHesap, gelirHesap, kdvHesap);
        await dbContext.SaveChangesAsync();

        var musteriKart = SatisBelgesiMuhasebeTestSupport.BuildCariKart(_uniqueSuffix, "MUS", CariKartTipleri.Musteri, _tesisId, musteriHesap.Id);
        musteriKart.EArsivKapsamindaMi = true;
        musteriKart.VergiNoTckn = "1111111111";
        // CariKartTipleri.Musteri, SatisBelgesiService.ApplyCariSnapshot tarafından "gerçek kişi"
        // olarak ele alınır (KurumsalMi, request'ten DEĞİL cari.CariTipi'nden türetilir) - bu
        // yüzden Faz 2B.4.2 kapısının ayrı ad/soyad/TCKN + yapısal adres kontrolleri CariKart
        // üzerinden karşılanmalıdır; CreateSatisBelgesiRequest'teki Musteri* alanları ApplyCariSnapshot
        // tarafından ZATEN ezilir.
        musteriKart.Ad = "Cutover";
        musteriKart.Soyad = "Musteri " + _uniqueSuffix;
        musteriKart.Ilce = "Beşiktaş";
        musteriKart.Il = "İstanbul";
        dbContext.CariKartlar.Add(musteriKart);
        await dbContext.SaveChangesAsync();
        _musteriKartId = musteriKart.Id;

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
            SeriKodu = SeriKodu,
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

    private static ISatisBelgesiService CreateService(StysAppDbContext dbContext, TimeProvider timeProvider, bool ubloptionsEnabled = true)
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
            new SatisBelgesiMuhasebeTestSupport.NoOpDomainOperationLogger(),
            timeProvider,
            Options.Create(new EBelgeUblOptions { Enabled = ubloptionsEnabled }),
            // Faz 2B.10 - bkz. SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService XML doc'u.
            kurumPolitikaServisi: EBelgeKurumPolitikaTestSupport.CreateAlwaysAktifServisi(dbContext, timeProvider));
    }

    private static StysAppDbContext CreateDbContext(int? currentKurumId = null, bool isSuperAdmin = true)
    {
        var optionsBuilder = new DbContextOptionsBuilder<StysAppDbContext>()
            .UseSqlServer(SatisBelgesiMuhasebeTestSupport.ConnectionString);

        return new StysAppDbContext(
            optionsBuilder.Options,
            new SatisBelgesiMuhasebeTestSupport.FakeCurrentUserAccessor(),
            new TestTenantAccessor(currentKurumId, isSuperAdmin));
    }

    private CreateSatisBelgesiRequest BuildSatisBelgesiRequest(DateTime belgeTarihi)
        => new()
        {
            BelgeNo = TruncateToMax($"{_uniqueSuffix}-{Guid.NewGuid():N}", 40),
            BelgeTipi = SatisBelgesiTipi.SatisFaturasi,
            TesisId = _tesisId,
            CariKartId = _musteriKartId,
            BelgeTarihi = belgeTarihi,
            // Musteri* alanları burada VERİLMEZ - CariKartId set edildiğinde
            // ApplyCariSnapshotToCreateRequest bunları musteriKart'tan (Ad/Soyad/Ilce/Il/VergiNoTckn)
            // ZATEN türetip ezer (bkz. InitializeAsync'teki musteriKart alan atamaları).
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

    private async Task<int> PrepareReadyInvoiceAsync(DateTime belgeTarihi)
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, TimeProvider.System);

        var created = await service.CreateAsync(BuildSatisBelgesiRequest(belgeTarihi), CancellationToken.None);
        await service.MuhasebeOnayinaGonderAsync(created.Id!.Value, CancellationToken.None);
        await service.MuhasebeOnaylaAsync(created.Id.Value, CancellationToken.None);

        var donemService = SatisBelgesiMuhasebeTestSupport.CreateRealMuhasebeDonemService(dbContext);
        var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext, donemService);
        await fisService.MuhasebeFisiOlusturAsync(created.Id.Value, CancellationToken.None);

        await using var verifyCtx = CreateDbContext();
        var muhasebeFisId = await verifyCtx.SatisBelgeleri
            .AsNoTracking()
            .Where(x => x.Id == created.Id.Value)
            .Select(x => x.MuhasebeFisId)
            .SingleAsync();
        Assert.True(muhasebeFisId.HasValue);

        return created.Id.Value;
    }

    private async Task<int> GetSayacOnceAsync()
    {
        await using var verifyCtx = CreateDbContext();
        return await verifyCtx.KurumFaturaNumaraSayaclari
            .AsNoTracking()
            .Where(x => x.KurumId == _kurumId && x.MaliYil == 2026 && x.SeriKodu == SeriKodu)
            .Select(x => x.SonNumara)
            .SingleAsync();
    }

    private async Task AssertNoCutArtifactsAsync(int satisBelgesiId, int expectedSayac)
    {
        await using var verifyCtx = CreateDbContext();

        var belge = await verifyCtx.SatisBelgeleri
            .AsNoTracking()
            .SingleAsync(x => x.Id == satisBelgesiId);

        Assert.Null(belge.ResmiFaturaNo);
        Assert.Null(belge.EBelgeUuid);
        Assert.Null(belge.FaturaKesimTarihi);
        Assert.Equal(TicariBelgeFaturalamaDurumu.KesimBekliyor, belge.FaturalamaDurumu);

        Assert.False(await verifyCtx.EBelgeKayitlari.IgnoreQueryFilters().AnyAsync(x => x.SatisBelgesiId == satisBelgesiId));
        Assert.False(await verifyCtx.EBelgeSnapshots.IgnoreQueryFilters().AnyAsync(x => x.EBelgeKaydi.SatisBelgesiId == satisBelgesiId));
        Assert.False(await verifyCtx.EBelgeOutboxMesajlari.IgnoreQueryFilters().AnyAsync(x => x.EBelgeKaydi.SatisBelgesiId == satisBelgesiId));

        var sayacSonra = await verifyCtx.KurumFaturaNumaraSayaclari
            .AsNoTracking()
            .SingleAsync(x => x.KurumId == _kurumId && x.MaliYil == 2026 && x.SeriKodu == SeriKodu);

        Assert.Equal(expectedSayac, sayacSonra.SonNumara);
    }

    private static string TruncateToMax(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];

    // 1. 13.09.2026 23:59:59.999 Türkiye saatinde fatura kesimi reddedilir.
    [IntegrationFact]
    public async Task Onuc09TrtSonAninda23_59_59_999daKesimReddedilir()
    {
        // TRT 13.09.2026 23:59:59.999 = UTC 13.09.2026 20:59:59.999 (UTC+3)
        var kesimUtc = new DateTimeOffset(2026, 9, 13, 20, 59, 59, 999, TimeSpan.Zero);
        var belgeId = await PrepareReadyInvoiceAsync(new DateTime(2026, 9, 14));
        var sayacOnce = await GetSayacOnceAsync();

        await using var cutCtx = CreateDbContext();
        var cutService = CreateService(cutCtx, new FakeTimeProvider(kesimUtc));

        var ex = await Assert.ThrowsAsync<EBelgeInvoiceDateBeforeGoLiveException>(
            () => cutService.FaturaKesAsync(belgeId, new FaturaKesRequest { SeriKodu = SeriKodu }, CancellationToken.None));

        Assert.Equal(EBelgeInvoiceDateBeforeGoLiveException.HttpStatusCode, ex.ErrorCode);
        await AssertNoCutArtifactsAsync(belgeId, sayacOnce);
    }

    // 2. 14.09.2026 00:00:00 Türkiye saatinde tarih kapısı geçilir.
    [IntegrationFact]
    public async Task OnDort09TrtBaslangicinda00_00_00daKapiGecilir()
    {
        // TRT 14.09.2026 00:00:00.000 = UTC 13.09.2026 21:00:00.000 (UTC+3)
        var kesimUtc = new DateTimeOffset(2026, 9, 13, 21, 0, 0, TimeSpan.Zero);
        var belgeId = await PrepareReadyInvoiceAsync(new DateTime(2026, 9, 14));

        await using var cutCtx = CreateDbContext();
        var cutService = CreateService(cutCtx, new FakeTimeProvider(kesimUtc));

        var cut = await cutService.FaturaKesAsync(belgeId, new FaturaKesRequest { SeriKodu = SeriKodu }, CancellationToken.None);

        Assert.NotNull(cut.ResmiFaturaNo);
    }

    // 3. UTC'den Türkiye saatine gün değişimi doğru değerlendirilir: UTC tarihi hâlâ 13.09.2026
    // olsa da (21:30 UTC), TRT karşılığı 14.09.2026 00:30 olduğu için kapı geçilmelidir.
    [IntegrationFact]
    public async Task UtcTarihiHalaOncekiGunKenTrtKarsiligiGoLiveGunuyseKapiGecilir()
    {
        var kesimUtc = new DateTimeOffset(2026, 9, 13, 21, 30, 0, TimeSpan.Zero);
        var belgeId = await PrepareReadyInvoiceAsync(new DateTime(2026, 9, 14));

        await using var cutCtx = CreateDbContext();
        var cutService = CreateService(cutCtx, new FakeTimeProvider(kesimUtc));

        var cut = await cutService.FaturaKesAsync(belgeId, new FaturaKesRequest { SeriKodu = SeriKodu }, CancellationToken.None);

        Assert.NotNull(cut.ResmiFaturaNo);
    }

    // 4 + 5. Reddedilen işlemde resmî fatura numarası tüketilmez ve FaturaKesimTarihi atanmaz
    // (AssertNoCutArtifactsAsync ResmiFaturaNo/FaturaKesimTarihi/sayaç üçünü birden doğrular).
    [IntegrationFact]
    public async Task ReddedilenIslemdeResmiNumaraTuketilmezVeFaturaKesimTarihiAtanmaz()
    {
        var kesimUtc = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero); // TRT 2026-09-01, go-live öncesi
        var belgeId = await PrepareReadyInvoiceAsync(new DateTime(2026, 9, 14));
        var sayacOnce = await GetSayacOnceAsync();

        await using var cutCtx = CreateDbContext();
        var cutService = CreateService(cutCtx, new FakeTimeProvider(kesimUtc));

        await Assert.ThrowsAsync<EBelgeInvoiceDateBeforeGoLiveException>(
            () => cutService.FaturaKesAsync(belgeId, new FaturaKesRequest { SeriKodu = SeriKodu }, CancellationToken.None));

        await AssertNoCutArtifactsAsync(belgeId, sayacOnce);
    }

    // 6. Başarılı işlemde kayıt tarihi ile kapının kullandığı zaman aynı TimeProvider anından gelir.
    [IntegrationFact]
    public async Task BasariliIslemdeFaturaKesimTarihiKapininKullandigiAyniTimeProviderAnindanGelir()
    {
        var kesimUtc = new DateTimeOffset(2026, 9, 20, 9, 15, 30, 123, TimeSpan.Zero);
        var belgeId = await PrepareReadyInvoiceAsync(new DateTime(2026, 9, 14));

        await using var cutCtx = CreateDbContext();
        var cutService = CreateService(cutCtx, new FakeTimeProvider(kesimUtc));

        await cutService.FaturaKesAsync(belgeId, new FaturaKesRequest { SeriKodu = SeriKodu }, CancellationToken.None);

        await using var verifyCtx = CreateDbContext();
        var belge = await verifyCtx.SatisBelgeleri.AsNoTracking().SingleAsync(x => x.Id == belgeId);

        Assert.Equal(kesimUtc.UtcDateTime, belge.FaturaKesimTarihi);
    }

    // Feature flag kapalıysa mevcut (bu fazdan önceki) davranış hiç değişmez: go-live öncesi
    // tarihte dahi kesim serbesttir.
    [IntegrationFact]
    public async Task FeatureFlagKapaliysaGoLiveOncesiTarihteKesimSerbesttir()
    {
        // BelgeTarihi/kesim, seeded MuhasebeDonem (2026) ve KurumFaturaNumaraSayaci (MaliYil=2026)
        // ile uyumlu kalması için 2026 içinde ama açıkça go-live (14.09.2026) ÖNCESİ seçilir.
        var kesimUtc = new DateTimeOffset(2026, 1, 10, 9, 0, 0, TimeSpan.Zero);
        var belgeId = await PrepareReadyInvoiceAsync(new DateTime(2026, 1, 10));

        await using var cutCtx = CreateDbContext();
        var cutService = CreateService(cutCtx, new FakeTimeProvider(kesimUtc), ubloptionsEnabled: false);

        var cut = await cutService.FaturaKesAsync(belgeId, new FaturaKesRequest { SeriKodu = SeriKodu }, CancellationToken.None);

        Assert.NotNull(cut.ResmiFaturaNo);
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _zaman;

        public FakeTimeProvider(DateTimeOffset zaman) => _zaman = zaman;

        public override DateTimeOffset GetUtcNow() => _zaman;
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
