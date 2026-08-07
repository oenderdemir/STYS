using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Kurumlar.Entities;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.CariKartlar.Mapping;
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
/// Faz 2B.10 görev md.10/md.11/md.12/md.19/md.20 - SatisBelgesiService.FaturaKesAsync akışının
/// kurum e-belge politikasına GÖRE yönlendirilmesini, immutable karar snapshot'ının tam
/// per-yöntem davranış matrisini VE tenant izolasyonunu, gerçek SQL Server'a karşı doğrular.
/// Bu dosya, GERÇEK <see cref="EBelgeKurumPolitikaServisi"/> ile (test-only "her zaman aktif"
/// fake'in AKSİNE) çalışır - böylece kurum politikasının GERÇEK yönlendirme etkisi test edilir.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SqlServerIntegrationCollection.Name)]
[Trait("Domain", "EBelge")]
[Trait("TestLevel", "SqlIntegration")]
[Trait("Dependency", "SqlServer")]
public class SatisBelgesiEBelgeKarariSaleFlowIntegrationTests : IAsyncLifetime
{
    private const string TestMarker = "EBF-SFL";
    private const string SeriKodu = "ESF";

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

        // Bu dosyanın testleri KENDİ kurum politikası senaryolarını kurar.
        await dbContext.Set<KurumEBelgePolitikasi>().IgnoreQueryFilters()
            .Where(p => p.KurumId == _kurumId)
            .ExecuteDeleteAsync();

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
        musteriKart.Ad = "SaleFlow";
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

    private static StysAppDbContext CreateDbContext() => SatisBelgesiMuhasebeTestSupport.CreateDbContext();

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

    /// <summary>GERÇEK EBelgeKurumPolitikaServisi (global kapı HER ZAMAN aktif - uzak geçmiş tarih) - kurum politikasının GERÇEK yönlendirme etkisini test eder.</summary>
    private static ISatisBelgesiService CreateService(StysAppDbContext dbContext, DateTimeOffset kesimZamani, bool globalAktif = true, bool ublEnabled = true)
    {
        var mapper = CreateMapper();
        var satisBelgesiRepository = new SatisBelgesiRepository(dbContext, mapper);
        var muhasebeFisRepository = new STYS.Muhasebe.MuhasebeFisleri.Repositories.MuhasebeFisRepository(dbContext, mapper);
        var timeProvider = new FakeTimeProvider(kesimZamani);
        var globalGate = new EBelgeProcessingActivationGate(
            Options.Create(globalAktif
                ? new EBelgeProcessingOptions { Enabled = true, NotBeforeLocalDate = "2020-01-01" }
                : new EBelgeProcessingOptions { Enabled = false }),
            timeProvider,
            NullLogger<EBelgeProcessingActivationGate>.Instance);
        var kurumPolitikaServisi = new EBelgeKurumPolitikaServisi(dbContext, globalGate, new EBelgeYontemYetenekSaglayici(), timeProvider);

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
            Options.Create(new EBelgeUblOptions { Enabled = ublEnabled }),
            kurumPolitikaServisi: kurumPolitikaServisi);
    }

    private CreateSatisBelgesiRequest BuildRequest()
        => new()
        {
            BelgeNo = TruncateToMax($"{_uniqueSuffix}-{Guid.NewGuid():N}", 40),
            BelgeTipi = SatisBelgesiTipi.SatisFaturasi,
            TesisId = _tesisId,
            CariKartId = _musteriKartId,
            BelgeTarihi = new DateTime(2026, 9, 20),
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

    private async Task<int> PrepareReadyInvoiceAsync(DateTimeOffset kesimZamani)
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, kesimZamani);

        var created = await service.CreateAsync(BuildRequest(), CancellationToken.None);
        await service.MuhasebeOnayinaGonderAsync(created.Id!.Value, CancellationToken.None);
        await service.MuhasebeOnaylaAsync(created.Id.Value, CancellationToken.None);

        var donemService = SatisBelgesiMuhasebeTestSupport.CreateRealMuhasebeDonemService(dbContext);
        var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext, donemService);
        await fisService.MuhasebeFisiOlusturAsync(created.Id.Value, CancellationToken.None);

        return created.Id.Value;
    }

    private static string TruncateToMax(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];

    private static readonly DateTimeOffset KesimZamani = new(2026, 9, 20, 9, 0, 0, TimeSpan.Zero);

    private async Task SeedPolitikaAsync(EBelgeEntegrasyonYontemi yontem, string? hariciSistemKodu = null)
    {
        await using var dbContext = CreateDbContext();
        dbContext.Add(new KurumEBelgePolitikasi
        {
            KurumId = _kurumId,
            EntegrasyonYontemi = yontem,
            AktifMi = true,
            AktivasyonYerelTarihi = new DateTime(2020, 1, 1),
            HariciSistemKodu = hariciSistemKodu,
            PolitikaSurumu = 1,
        });
        await dbContext.SaveChangesAsync();
    }

    // ---- Kullanilmayacak: karar oluşur, yerel pipeline HİÇ oluşmaz ----

    [IntegrationFact]
    public async Task KullanilmayacakPolitikasiKararOlusturarakSatisiTamamlarAmaYerelPipelineOlusturmaz()
    {
        await SeedPolitikaAsync(EBelgeEntegrasyonYontemi.Kullanilmayacak);
        var belgeId = await PrepareReadyInvoiceAsync(KesimZamani);

        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, KesimZamani);
        var sonuc = await service.FaturaKesAsync(belgeId, new FaturaKesRequest { SeriKodu = SeriKodu }, CancellationToken.None);

        Assert.NotNull(sonuc.ResmiFaturaNo);

        await using var verifyCtx = CreateDbContext();
        var karar = await verifyCtx.SatisBelgesiEBelgeKararlari.AsNoTracking().SingleAsync(x => x.SatisBelgesiId == belgeId);
        Assert.Equal(EBelgeKurumPolitikaKararNedeni.YontemKullanilmayacak, karar.KararNedeni);
        Assert.False(karar.YerelSnapshotOlustur);
        Assert.False(karar.YerelUnsignedUblOlustur);
        Assert.False(karar.YerelImzaOlustur);
        Assert.Null(karar.EBelgeKaydiId);

        Assert.False(await verifyCtx.EBelgeKayitlari.IgnoreQueryFilters().AnyAsync(x => x.SatisBelgesiId == belgeId));
    }

    // ---- HariciMuhasebeSistemi: aynı şekilde, dış sistem adaptörü BU FAZDA çağrılmaz ----

    [IntegrationFact]
    public async Task HariciMuhasebeSistemiPolitikasiKararOlusturarakSatisiTamamlarAmaYerelPipelineOlusturmaz()
    {
        await SeedPolitikaAsync(EBelgeEntegrasyonYontemi.HariciMuhasebeSistemi, hariciSistemKodu: "ERP-42");
        var belgeId = await PrepareReadyInvoiceAsync(KesimZamani);

        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, KesimZamani);
        var sonuc = await service.FaturaKesAsync(belgeId, new FaturaKesRequest { SeriKodu = SeriKodu }, CancellationToken.None);

        Assert.NotNull(sonuc.ResmiFaturaNo);

        await using var verifyCtx = CreateDbContext();
        var karar = await verifyCtx.SatisBelgesiEBelgeKararlari.AsNoTracking().SingleAsync(x => x.SatisBelgesiId == belgeId);
        Assert.Equal(EBelgeKurumPolitikaKararNedeni.HariciSistemSorumlu, karar.KararNedeni);
        Assert.False(await verifyCtx.EBelgeKayitlari.IgnoreQueryFilters().AnyAsync(x => x.SatisBelgesiId == belgeId));
    }

    // ---- GibPortal: karar + EBelgeKaydi + snapshot pipeline oluşur, ama ASLA imza mesajı ----

    [IntegrationFact]
    [Trait("CriticalInvariant", "PortalRouteNeverSignsLocally")]
    public async Task GibPortalPolitikasiYerelSnapshotVeUnsignedUblUretirAmaAslaImzaMesajiOlusturmaz()
    {
        await SeedPolitikaAsync(EBelgeEntegrasyonYontemi.GibPortal);
        var belgeId = await PrepareReadyInvoiceAsync(KesimZamani);

        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, KesimZamani);
        var sonuc = await service.FaturaKesAsync(belgeId, new FaturaKesRequest { SeriKodu = SeriKodu }, CancellationToken.None);

        Assert.NotNull(sonuc.ResmiFaturaNo);

        await using var verifyCtx = CreateDbContext();
        var karar = await verifyCtx.SatisBelgesiEBelgeKararlari.AsNoTracking().SingleAsync(x => x.SatisBelgesiId == belgeId);
        Assert.Equal(EBelgeKurumPolitikaKararNedeni.Aktif, karar.KararNedeni);
        Assert.Equal(EBelgeEntegrasyonYontemi.GibPortal, karar.EntegrasyonYontemi);
        Assert.True(karar.YerelSnapshotOlustur);
        Assert.True(karar.YerelUnsignedUblOlustur);
        Assert.False(karar.YerelImzaOlustur);
        Assert.NotNull(karar.EBelgeKaydiId);

        var eBelgeKaydi = await verifyCtx.EBelgeKayitlari.AsNoTracking().SingleAsync(x => x.SatisBelgesiId == belgeId);
        Assert.Equal(eBelgeKaydi.Id, karar.EBelgeKaydiId);

        // md.11/md.12 - ArtefaktOlustur outbox mesajı oluşur, ama ASLA bir UblImzala mesajı OLUŞMAZ.
        var isTurleri = await verifyCtx.EBelgeOutboxMesajlari.IgnoreQueryFilters()
            .Where(x => x.EBelgeKaydiId == eBelgeKaydi.Id)
            .Select(x => x.IsTuru)
            .ToListAsync();
        Assert.Contains(EBelgeOutboxIsTuru.ArtefaktOlustur, isTurleri);
        Assert.DoesNotContain(EBelgeOutboxIsTuru.UblImzala, isTurleri);
    }

    // ---- Faz 2B.11.1 görev md.4/md.7 - UBL feature flag runtime fail-closed garantisi ----

    [IntegrationFact]
    public async Task GibPortalUblDisabledIkenFaturaKesAsyncFailClosedReddederYanEtkiOlusmaz()
    {
        await SeedPolitikaAsync(EBelgeEntegrasyonYontemi.GibPortal);
        var belgeId = await PrepareReadyInvoiceAsync(KesimZamani);

        await using var sayacOncesiCtx = CreateDbContext();
        var sayacOncesi = await sayacOncesiCtx.KurumFaturaNumaraSayaclari.AsNoTracking()
            .SingleAsync(x => x.KurumId == _kurumId && x.MaliYil == 2026 && x.SeriKodu == SeriKodu);

        await using var dbContext = CreateDbContext();
        // Faz 2B.11.1 görev md.4/md.7 - GibPortal politikası YerelUnsignedUblOlustur=true İSTER,
        // ama EBelgeUblOptions.Enabled BURADA kapalı - runtime fail-closed reddetmelidir.
        var service = CreateService(dbContext, KesimZamani, ublEnabled: false);

        await Assert.ThrowsAsync<EBelgeUblFeatureDisabledException>(
            () => service.FaturaKesAsync(belgeId, new FaturaKesRequest { SeriKodu = SeriKodu }, CancellationToken.None));

        await using var verifyCtx = CreateDbContext();
        var belge = await verifyCtx.SatisBelgeleri.AsNoTracking().SingleAsync(x => x.Id == belgeId);
        Assert.Null(belge.ResmiFaturaNo);

        var sayacSonrasi = await verifyCtx.KurumFaturaNumaraSayaclari.AsNoTracking()
            .SingleAsync(x => x.KurumId == _kurumId && x.MaliYil == 2026 && x.SeriKodu == SeriKodu);
        Assert.Equal(sayacOncesi.SonNumara, sayacSonrasi.SonNumara); // sayaç TÜKETİLMEDİ

        Assert.False(await verifyCtx.SatisBelgesiEBelgeKararlari.IgnoreQueryFilters().AnyAsync(x => x.SatisBelgesiId == belgeId));
        Assert.False(await verifyCtx.EBelgeKayitlari.IgnoreQueryFilters().AnyAsync(x => x.SatisBelgesiId == belgeId));
        Assert.False(await verifyCtx.EBelgeSnapshots.IgnoreQueryFilters().AnyAsync(x => x.KurumId == _kurumId));
        Assert.False(await verifyCtx.EBelgeOutboxMesajlari.IgnoreQueryFilters().AnyAsync(x => x.KurumId == _kurumId));
    }

    [IntegrationFact]
    public async Task GibPortalUblEnabledIkenV2SnapshotVeOutboxUretmeyeDevamEder()
    {
        await SeedPolitikaAsync(EBelgeEntegrasyonYontemi.GibPortal);
        var belgeId = await PrepareReadyInvoiceAsync(KesimZamani);

        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, KesimZamani, ublEnabled: true);
        var sonuc = await service.FaturaKesAsync(belgeId, new FaturaKesRequest { SeriKodu = SeriKodu }, CancellationToken.None);

        Assert.NotNull(sonuc.ResmiFaturaNo);

        await using var verifyCtx = CreateDbContext();
        var eBelgeKaydi = await verifyCtx.EBelgeKayitlari.AsNoTracking().SingleAsync(x => x.SatisBelgesiId == belgeId);
        var snapshot = await verifyCtx.EBelgeSnapshots.AsNoTracking().SingleAsync(x => x.EBelgeKaydiId == eBelgeKaydi.Id);
        Assert.Equal("2", snapshot.SnapshotSchemaVersion); // V1 fallback DEĞİL - deterministik V2

        var isTurleri = await verifyCtx.EBelgeOutboxMesajlari.IgnoreQueryFilters()
            .Where(x => x.EBelgeKaydiId == eBelgeKaydi.Id)
            .Select(x => x.IsTuru)
            .ToListAsync();
        Assert.Contains(EBelgeOutboxIsTuru.ArtefaktOlustur, isTurleri);
    }

    // ---- Faz 2B.11.1 görev md.7 - non-local yöntemler UBL flag'inden ETKİLENMEZ (regresyon) ----

    [IntegrationFact]
    public async Task KullanilmayacakUblDisabledIkenNormalSatisAkisiniSurdurur()
    {
        await SeedPolitikaAsync(EBelgeEntegrasyonYontemi.Kullanilmayacak);
        var belgeId = await PrepareReadyInvoiceAsync(KesimZamani);

        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, KesimZamani, ublEnabled: false);
        var sonuc = await service.FaturaKesAsync(belgeId, new FaturaKesRequest { SeriKodu = SeriKodu }, CancellationToken.None);

        Assert.NotNull(sonuc.ResmiFaturaNo);

        await using var verifyCtx = CreateDbContext();
        var karar = await verifyCtx.SatisBelgesiEBelgeKararlari.AsNoTracking().SingleAsync(x => x.SatisBelgesiId == belgeId);
        Assert.False(karar.YerelSnapshotOlustur);
        Assert.False(await verifyCtx.EBelgeKayitlari.IgnoreQueryFilters().AnyAsync(x => x.SatisBelgesiId == belgeId));
    }

    [IntegrationFact]
    public async Task HariciMuhasebeSistemiUblDisabledIkenNormalSatisAkisiniSurdurur()
    {
        await SeedPolitikaAsync(EBelgeEntegrasyonYontemi.HariciMuhasebeSistemi, hariciSistemKodu: "ERP-42");
        var belgeId = await PrepareReadyInvoiceAsync(KesimZamani);

        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, KesimZamani, ublEnabled: false);
        var sonuc = await service.FaturaKesAsync(belgeId, new FaturaKesRequest { SeriKodu = SeriKodu }, CancellationToken.None);

        Assert.NotNull(sonuc.ResmiFaturaNo);

        await using var verifyCtx = CreateDbContext();
        var karar = await verifyCtx.SatisBelgesiEBelgeKararlari.AsNoTracking().SingleAsync(x => x.SatisBelgesiId == belgeId);
        Assert.False(karar.YerelSnapshotOlustur);
        Assert.False(await verifyCtx.EBelgeKayitlari.IgnoreQueryFilters().AnyAsync(x => x.SatisBelgesiId == belgeId));
    }

    // ---- Faz 2B.10.1 görev md.9 - yerel pipeline GEREKTİRMEYEN yöntemlerde ikinci FaturaKesAsync idempotent döner ----

    public static IEnumerable<object[]> YerelPipelineGerektirmeyenSenaryolar() =>
    [
        ["Kullanilmayacak"],
        ["HariciMuhasebeSistemi"],
        ["GlobalKapali"],
    ];

    [IntegrationTheory]
    [Trait("CriticalInvariant", "NonLocalRouteIsIdempotent")]
    [MemberData(nameof(YerelPipelineGerektirmeyenSenaryolar))]
    public async Task YerelPipelineGerektirmeyenYontemlerdeIkinciKesimIdempotentDonerSayacTuketmezIkinciKararOlusturmaz(string senaryo)
    {
        var globalAktif = senaryo != "GlobalKapali";
        if (globalAktif)
        {
            var yontem = senaryo == "Kullanilmayacak" ? EBelgeEntegrasyonYontemi.Kullanilmayacak : EBelgeEntegrasyonYontemi.HariciMuhasebeSistemi;
            await SeedPolitikaAsync(yontem, hariciSistemKodu: yontem == EBelgeEntegrasyonYontemi.HariciMuhasebeSistemi ? "ERP-42" : null);
        }
        // GlobalKapali senaryosunda politika HİÇ seed edilmez - global kapı zaten kapalı olduğundan
        // kurum politikası hiç değerlendirilmeden aynı fail-open-olmayan sonuca ulaşılır.

        var belgeId = await PrepareReadyInvoiceAsync(KesimZamani);

        await using var ilkCtx = CreateDbContext();
        var ilkService = CreateService(ilkCtx, KesimZamani, globalAktif: globalAktif);
        var ilkSonuc = await ilkService.FaturaKesAsync(belgeId, new FaturaKesRequest { SeriKodu = SeriKodu }, CancellationToken.None);
        Assert.NotNull(ilkSonuc.ResmiFaturaNo);

        await using var sayacCtx1 = CreateDbContext();
        var sayacIlk = await sayacCtx1.KurumFaturaNumaraSayaclari.AsNoTracking()
            .SingleAsync(x => x.KurumId == _kurumId && x.MaliYil == 2026 && x.SeriKodu == SeriKodu);

        // İKİNCİ FaturaKesAsync çağrısı - ÖNCEDEN "EBelgeKaydi bulunamadı" veri-tutarsızlığı
        // hatası veriyordu (bkz. görev md.4); artık idempotent olarak mevcut sonucu DÖNMELİDİR.
        await using var ikinciCtx = CreateDbContext();
        var ikinciService = CreateService(ikinciCtx, KesimZamani, globalAktif: globalAktif);
        var ikinciSonuc = await ikinciService.FaturaKesAsync(belgeId, new FaturaKesRequest { SeriKodu = SeriKodu }, CancellationToken.None);

        Assert.Equal(ilkSonuc.ResmiFaturaNo, ikinciSonuc.ResmiFaturaNo);

        await using var sayacCtx2 = CreateDbContext();
        var sayacIkinci = await sayacCtx2.KurumFaturaNumaraSayaclari.AsNoTracking()
            .SingleAsync(x => x.KurumId == _kurumId && x.MaliYil == 2026 && x.SeriKodu == SeriKodu);
        Assert.Equal(sayacIlk.SonNumara, sayacIkinci.SonNumara); // sayaç TÜKETİLMEDİ

        await using var kararCtx = CreateDbContext();
        var kararSayisi = await kararCtx.SatisBelgesiEBelgeKararlari.IgnoreQueryFilters().CountAsync(x => x.SatisBelgesiId == belgeId);
        Assert.Equal(1, kararSayisi); // ikinci karar OLUŞTURULMADI
    }

    // ---- GibPortal İÇİN mevcut EBelgeKaydi/snapshot/outbox kontrolü korunur ----

    [IntegrationFact]
    public async Task GibPortalIkinciKesimMevcutEBelgeKaydiSnapshotOutboxKontroluyleIdempotentDoner()
    {
        await SeedPolitikaAsync(EBelgeEntegrasyonYontemi.GibPortal);
        var belgeId = await PrepareReadyInvoiceAsync(KesimZamani);

        await using var ilkCtx = CreateDbContext();
        var ilkService = CreateService(ilkCtx, KesimZamani);
        var ilkSonuc = await ilkService.FaturaKesAsync(belgeId, new FaturaKesRequest { SeriKodu = SeriKodu }, CancellationToken.None);

        await using var ikinciCtx = CreateDbContext();
        var ikinciService = CreateService(ikinciCtx, KesimZamani);
        var ikinciSonuc = await ikinciService.FaturaKesAsync(belgeId, new FaturaKesRequest { SeriKodu = SeriKodu }, CancellationToken.None);

        Assert.Equal(ilkSonuc.ResmiFaturaNo, ikinciSonuc.ResmiFaturaNo);

        await using var verifyCtx = CreateDbContext();
        var eBelgeKaydiSayisi = await verifyCtx.EBelgeKayitlari.IgnoreQueryFilters().CountAsync(x => x.SatisBelgesiId == belgeId);
        Assert.Equal(1, eBelgeKaydiSayisi);
        var kararSayisi = await verifyCtx.SatisBelgesiEBelgeKararlari.IgnoreQueryFilters().CountAsync(x => x.SatisBelgesiId == belgeId);
        Assert.Equal(1, kararSayisi);
    }

    // ---- Immutable karar yoksa (Faz 2B.10 öncesi/legacy) idempotent tekrar fail-closed olur ----

    [IntegrationFact]
    public async Task ImmutableKararYoksaIdempotentTekrarFailClosedOlur()
    {
        await SeedPolitikaAsync(EBelgeEntegrasyonYontemi.Kullanilmayacak);
        var belgeId = await PrepareReadyInvoiceAsync(KesimZamani);

        await using var ilkCtx = CreateDbContext();
        var ilkService = CreateService(ilkCtx, KesimZamani);
        await ilkService.FaturaKesAsync(belgeId, new FaturaKesRequest { SeriKodu = SeriKodu }, CancellationToken.None);

        // Faz 2B.10 ÖNCESİ (legacy) kesilmiş bir belgeyi simüle etmek İÇİN, immutable karar
        // KENDİSİ SİLİNEMEZ (immutability korumalı) - bu yüzden doğrudan SQL İLE (uygulama
        // katmanını BYPASS ederek) silinir.
        await using var legacySimCtx = CreateDbContext();
        await legacySimCtx.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM [muhasebe].[SatisBelgesiEBelgeKararlari] WHERE [SatisBelgesiId] = {belgeId}");

        await using var ikinciCtx = CreateDbContext();
        var ikinciService = CreateService(ikinciCtx, KesimZamani);

        var ex = await Assert.ThrowsAsync<EBelgeKurumPolitikaKararBulunamadiException>(
            () => ikinciService.FaturaKesAsync(belgeId, new FaturaKesRequest { SeriKodu = SeriKodu }, CancellationToken.None));

        Assert.Equal("EBELGE_KURUM_POLICY_DECISION_NOT_FOUND", ex.HataKodu);
    }

    // ---- Faz 2B.10.1 görev md.10 - yerel pipeline GEREKTİRMEYEN yöntemlerde UBL'ye özgü alanlar zorunlu DEĞİL ----

    [IntegrationFact]
    public async Task KullanilmayacakYontemindeCariKartEFaturaEArsivBayraklariKapaliOlsaDaSatisKesilebilir()
    {
        await SeedPolitikaAsync(EBelgeEntegrasyonYontemi.Kullanilmayacak);
        var belgeId = await PrepareReadyInvoiceAsync(KesimZamani);

        // Cari kartın e-Fatura/e-Arşiv bayraklarını KAPAT - GibPortal/yerel pipeline gerekseydi
        // ResolveEBelgeKanali bunu REDDEDERDİ.
        await using var mutateCtx = CreateDbContext();
        var cariKart = await mutateCtx.CariKartlar.SingleAsync(x => x.Id == _musteriKartId);
        cariKart.EArsivKapsamindaMi = false;
        cariKart.EFaturaMukellefiMi = false;
        await mutateCtx.SaveChangesAsync();

        await using var kesimCtx = CreateDbContext();
        var service = CreateService(kesimCtx, KesimZamani);
        var sonuc = await service.FaturaKesAsync(belgeId, new FaturaKesRequest { SeriKodu = SeriKodu }, CancellationToken.None);

        Assert.NotNull(sonuc.ResmiFaturaNo);
    }

    [IntegrationFact]
    public async Task HariciMuhasebeSistemiYontemindeKurumAdresAlaniEksikOlsaDaSatisKesilebilir()
    {
        await SeedPolitikaAsync(EBelgeEntegrasyonYontemi.HariciMuhasebeSistemi, hariciSistemKodu: "ERP-42");
        var belgeId = await PrepareReadyInvoiceAsync(KesimZamani);

        // Kurumun UBL'ye özgü hukuki adres alanını BOŞALT - yerel pipeline gerekseydi
        // EnsureUblHazirlikKaynaklari bunu REDDEDERDİ.
        await using var mutateCtx = CreateDbContext();
        var kurum = await mutateCtx.Kurumlar.SingleAsync(x => x.Id == _kurumId);
        kurum.Adres = "";
        await mutateCtx.SaveChangesAsync();

        await using var kesimCtx = CreateDbContext();
        var service = CreateService(kesimCtx, KesimZamani);
        var sonuc = await service.FaturaKesAsync(belgeId, new FaturaKesRequest { SeriKodu = SeriKodu }, CancellationToken.None);

        Assert.NotNull(sonuc.ResmiFaturaNo);

        await using var restoreCtx = CreateDbContext();
        var kurumRestore = await restoreCtx.Kurumlar.SingleAsync(x => x.Id == _kurumId);
        kurumRestore.Adres = "Test Kurum Adres " + _uniqueSuffix;
        await restoreCtx.SaveChangesAsync();
    }

    [IntegrationFact]
    public async Task GibPortalYontemindeUblAlanlariHalaZorunludurVeKanalDogruCozulur()
    {
        await SeedPolitikaAsync(EBelgeEntegrasyonYontemi.GibPortal);
        var belgeId = await PrepareReadyInvoiceAsync(KesimZamani);

        await using var mutateCtx = CreateDbContext();
        var cariKart = await mutateCtx.CariKartlar.SingleAsync(x => x.Id == _musteriKartId);
        cariKart.EArsivKapsamindaMi = false;
        cariKart.EFaturaMukellefiMi = false;
        await mutateCtx.SaveChangesAsync();

        await using var kesimCtx = CreateDbContext();
        var service = CreateService(kesimCtx, KesimZamani);

        await Assert.ThrowsAsync<BaseException>(
            () => service.FaturaKesAsync(belgeId, new FaturaKesRequest { SeriKodu = SeriKodu }, CancellationToken.None));

        await using var verifyCtx = CreateDbContext();
        var belge = await verifyCtx.SatisBelgeleri.AsNoTracking().SingleAsync(x => x.Id == belgeId);
        Assert.Null(belge.ResmiFaturaNo);
    }

    // ---- Politika değerlendirmesi İLE karar persist edilmesi ARASINDA politika sürümü değişirse rollback ----

    /// <summary>
    /// Faz 2B.10.2 görev md.8/md.12 - GERÇEK İKİ `StysAppDbContext` (Context A: satış akışı,
    /// Context B: "eşzamanlı admin PUT'u" side-effect'i) kullanır - `DegerlendirAsync`'in kendisi
    /// SATIŞ akışının aynı transaction'ının PARÇASI olduğundan (ve unlocked bir SELECT olduğundan),
    /// Context B'nin güncellemesini `DegerlendirAsync` ÇAĞRISI TAMAMLANDIKTAN HEMEN SONRA ama satış
    /// akışının kendi `IEBelgeKurumPolitikaTransactionGuard.KilitleVeOkuAsync` kilidini almasından
    /// ÖNCE, KISA ÖMÜRLÜ ve HEMEN COMMIT EDİLEN bir transaction ile uygulayan sahte bir karar
    /// servisi (`PolitikaSurumunuDegistirenKarariServisi`) kullanılır - bu, gerçek iki-transaction
    /// bir yarışı DETERMİNİSTİK biçimde kurar (Context B'nin update'i Context A'nın GUARD kilidini
    /// almasından ÖNCE zaten COMMIT EDİLMİŞ olur, böylece guard GÜNCEL - değişmiş - satırı görür).
    /// Artık eski, salt `PolitikaSurumu` sütununu unlocked yeniden okuyan kontrol DEĞİL, YENİ
    /// guard-tabanlı kilit+çok-alanlı karşılaştırma test edilir.
    /// </summary>
    [IntegrationFact]
    [Trait("CriticalInvariant", "PolicyDecisionVersionIsSerialized")]
    public async Task PolitikaSurumuKararDegerlendirmesiIlePersistArasindaDegisirseTumSatisKesimiRollbackOlur()
    {
        await SeedPolitikaAsync(EBelgeEntegrasyonYontemi.Kullanilmayacak);
        var belgeId = await PrepareReadyInvoiceAsync(KesimZamani);

        await using var sayacCtx = CreateDbContext();
        var sayacOnce = await sayacCtx.KurumFaturaNumaraSayaclari.AsNoTracking()
            .SingleAsync(x => x.KurumId == _kurumId && x.MaliYil == 2026 && x.SeriKodu == SeriKodu);

        // Yarışı simüle etmek İÇİN: gerçek servisin DegerlendirAsync'i çağırdığı ANDAN SONRA ama
        // guard kilidinden ÖNCE politika sürümünü değiştiren bir SAHTE karar servisi kullanılır -
        // gerçek DegerlendirAsync SONUCUNU (eski sürüm bilgisiyle) döner, ama YAN ETKİ olarak
        // politikayı YENİ bir sürüme yükseltir (başka bir oturumun eşzamanlı PUT'unu simüle eder).
        await using var kesimCtx = CreateDbContext();
        var mapper = CreateMapper();
        var satisBelgesiRepository = new SatisBelgesiRepository(kesimCtx, mapper);
        var muhasebeFisRepository = new STYS.Muhasebe.MuhasebeFisleri.Repositories.MuhasebeFisRepository(kesimCtx, mapper);
        var timeProvider = new FakeTimeProvider(KesimZamani);
        var globalGate = new EBelgeProcessingActivationGate(
            Options.Create(new EBelgeProcessingOptions { Enabled = true, NotBeforeLocalDate = "2020-01-01" }),
            timeProvider,
            NullLogger<EBelgeProcessingActivationGate>.Instance);
        var gercekPolitikaServisi = new EBelgeKurumPolitikaServisi(kesimCtx, globalGate, new EBelgeYontemYetenekSaglayici(), timeProvider);
        var yarisServisi = new PolitikaSurumunuDegistirenKarariServisi(gercekPolitikaServisi, _kurumId);

        var service = new SatisBelgesiService(
            satisBelgesiRepository,
            kesimCtx,
            mapper,
            muhasebeFisRepository,
            null!,
            new SatisBelgesiMuhasebeTestSupport.FakeUserAccessScopeService(),
            NullLogger<SatisBelgesiService>.Instance,
            new SatisBelgesiMuhasebeTestSupport.NoOpDomainOperationLogger(),
            timeProvider,
            Options.Create(new EBelgeUblOptions { Enabled = true }),
            kurumPolitikaServisi: yarisServisi);

        await Assert.ThrowsAsync<EBelgeKurumPolitikaKararCakismasiException>(
            () => service.FaturaKesAsync(belgeId, new FaturaKesRequest { SeriKodu = SeriKodu }, CancellationToken.None));

        await using var verifyCtx = CreateDbContext();
        var belge = await verifyCtx.SatisBelgeleri.AsNoTracking().SingleAsync(x => x.Id == belgeId);
        Assert.Null(belge.ResmiFaturaNo); // rollback oldu

        var sayacSonra = await verifyCtx.KurumFaturaNumaraSayaclari.AsNoTracking()
            .SingleAsync(x => x.KurumId == _kurumId && x.MaliYil == 2026 && x.SeriKodu == SeriKodu);
        Assert.Equal(sayacOnce.SonNumara, sayacSonra.SonNumara); // sayaç TÜKETİLMEDİ

        var kararSayisi = await verifyCtx.SatisBelgesiEBelgeKararlari.IgnoreQueryFilters().CountAsync(x => x.SatisBelgesiId == belgeId);
        Assert.Equal(0, kararSayisi); // immutable karar OLUŞTURULMADI
    }

    /// <summary>DegerlendirAsync'i gerçek servise devreder, ama YAN ETKİ olarak (dönmeden ÖNCE) politikanın PolitikaSurumu'nu artırır - "değerlendirme İLE persist ARASINDA politika değişti" yarışını deterministik olarak simüle eder.</summary>
    private sealed class PolitikaSurumunuDegistirenKarariServisi : IEBelgeKurumPolitikaServisi
    {
        private readonly IEBelgeKurumPolitikaServisi _inner;
        private readonly int _kurumId;
        private bool _degistirildi;

        public PolitikaSurumunuDegistirenKarariServisi(IEBelgeKurumPolitikaServisi inner, int kurumId)
        {
            _inner = inner;
            _kurumId = kurumId;
        }

        public async Task<EBelgeKurumPolitikaKarari> DegerlendirAsync(int kurumId, DateTime belgeTarihi, CancellationToken cancellationToken = default)
        {
            var sonuc = await _inner.DegerlendirAsync(kurumId, belgeTarihi, cancellationToken);

            if (!_degistirildi)
            {
                _degistirildi = true;
                await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
                await dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE [muhasebe].[KurumEBelgePolitikalari] SET [PolitikaSurumu] = [PolitikaSurumu] + 1 WHERE [KurumId] = {_kurumId}", cancellationToken);
            }

            return sonuc;
        }

        public Task<EBelgeIslemPolitikaUygunlukSonucu> DegerlendirIslemUygunlugunuAsync(int kurumId, int eBelgeKaydiId, EBelgeOutboxIsTuru isTuru, CancellationToken cancellationToken = default) =>
            _inner.DegerlendirIslemUygunlugunuAsync(kurumId, eBelgeKaydiId, isTuru, cancellationToken);

        public Task<EBelgeIslemPolitikaUygunlukSonucu> DegerlendirIslemUygunlugunuKilitliAsync(int kurumId, int eBelgeKaydiId, EBelgeOutboxIsTuru isTuru, EBelgeKilitliPolitikaSnapshot? kilitliPolitika, CancellationToken cancellationToken = default) =>
            _inner.DegerlendirIslemUygunlugunuKilitliAsync(kurumId, eBelgeKaydiId, isTuru, kilitliPolitika, cancellationToken);
    }

    // ---- Politika yapılandırılmadıysa: satış TAMAMLANMAZ, hiçbir kalıcı iz bırakmaz ----

    [IntegrationFact]
    public async Task PolitikaHicYapilandirilmadiysaFaturaKesimiReddedilirVeHicbirIzBirakmaz()
    {
        // Politika HİÇ seed edilmedi.
        var belgeId = await PrepareReadyInvoiceAsync(KesimZamani);

        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, KesimZamani);

        var ex = await Assert.ThrowsAsync<EBelgeKurumPolitikaEngelliException>(
            () => service.FaturaKesAsync(belgeId, new FaturaKesRequest { SeriKodu = SeriKodu }, CancellationToken.None));

        Assert.Equal("EBELGE_KURUM_POLICY_NOT_CONFIGURED", ex.HataKodu);

        await using var verifyCtx = CreateDbContext();
        var belge = await verifyCtx.SatisBelgeleri.AsNoTracking().SingleAsync(x => x.Id == belgeId);
        Assert.Null(belge.ResmiFaturaNo);
        Assert.False(await verifyCtx.SatisBelgesiEBelgeKararlari.IgnoreQueryFilters().AnyAsync(x => x.SatisBelgesiId == belgeId));
        Assert.False(await verifyCtx.EBelgeKayitlari.IgnoreQueryFilters().AnyAsync(x => x.SatisBelgesiId == belgeId));
    }

    // ---- Uniqueness/immutability: aynı (KurumId, SatisBelgesiId) için ikinci bir karar SATIRI EKLENEMEZ ----

    [IntegrationFact]
    public async Task AyniSatisBelgesiIcinIkinciKararSatiriBenzersizlikIhlaliVerir()
    {
        await SeedPolitikaAsync(EBelgeEntegrasyonYontemi.Kullanilmayacak);
        var belgeId = await PrepareReadyInvoiceAsync(KesimZamani);

        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, KesimZamani);
        await service.FaturaKesAsync(belgeId, new FaturaKesRequest { SeriKodu = SeriKodu }, CancellationToken.None);

        await using var ikinciCtx = CreateDbContext();
        ikinciCtx.Add(new SatisBelgesiEBelgeKarari
        {
            KurumId = _kurumId,
            SatisBelgesiId = belgeId,
            EntegrasyonYontemi = EBelgeEntegrasyonYontemi.Kullanilmayacak,
            KararNedeni = EBelgeKurumPolitikaKararNedeni.YontemKullanilmayacak,
            KararZamaniUtc = DateTime.UtcNow,
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => ikinciCtx.SaveChangesAsync());
    }

    // ---- Immutability: mevcut karar update/delete EDİLEMEZ ----

    [IntegrationFact]
    public async Task MevcutKararGuncellenemezVeSilinemez()
    {
        await SeedPolitikaAsync(EBelgeEntegrasyonYontemi.Kullanilmayacak);
        var belgeId = await PrepareReadyInvoiceAsync(KesimZamani);

        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, KesimZamani);
        await service.FaturaKesAsync(belgeId, new FaturaKesRequest { SeriKodu = SeriKodu }, CancellationToken.None);

        await using var guncelleCtx = CreateDbContext();
        var karar = await guncelleCtx.SatisBelgesiEBelgeKararlari.SingleAsync(x => x.SatisBelgesiId == belgeId);
        karar.KararNedeni = EBelgeKurumPolitikaKararNedeni.Aktif;

        await Assert.ThrowsAsync<BaseException>(() => guncelleCtx.SaveChangesAsync());

        await using var silCtx = CreateDbContext();
        var kararSil = await silCtx.SatisBelgesiEBelgeKararlari.SingleAsync(x => x.SatisBelgesiId == belgeId);
        silCtx.Remove(kararSil);

        await Assert.ThrowsAsync<BaseException>(() => silCtx.SaveChangesAsync());
    }

    // ---- Tenant izolasyonu: karar, BAŞKA bir kurumun satış belgesine bağlanamaz ----

    [IntegrationFact]
    [Trait("CriticalInvariant", "InstitutionPolicyTenantIsolation")]
    public async Task KararBaskaBirKurumunSatisBelgesineBaglanamaz()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var (kurumB, ilB, tesisB) = await SatisBelgesiMuhasebeTestSupport.SeedKurumIlTesisAsync(dbContext, _uniqueSuffix + "-B");

        try
        {
            await SeedPolitikaAsync(EBelgeEntegrasyonYontemi.Kullanilmayacak);
            var belgeId = await PrepareReadyInvoiceAsync(KesimZamani); // _kurumId'ye ait

            await using var yanlisCtx = CreateDbContext();
            // Kurum B için, ama KURUM A'nın satış belgesine işaret eden bir karar denemesi.
            yanlisCtx.Add(new SatisBelgesiEBelgeKarari
            {
                KurumId = kurumB.Id,
                SatisBelgesiId = belgeId,
                EntegrasyonYontemi = EBelgeEntegrasyonYontemi.Kullanilmayacak,
                KararNedeni = EBelgeKurumPolitikaKararNedeni.YontemKullanilmayacak,
                KararZamaniUtc = DateTime.UtcNow,
            });

            await Assert.ThrowsAsync<DbUpdateException>(() => yanlisCtx.SaveChangesAsync());
        }
        finally
        {
            await using var cleanupCtx = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
            await SatisBelgesiMuhasebeTestSupport.CleanupAsync(cleanupCtx, _uniqueSuffix + "-B", tesisB.Id, kurumB.Id, ilB.Id);
        }
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _zaman;

        public FakeTimeProvider(DateTimeOffset zaman) => _zaman = zaman;

        public override DateTimeOffset GetUtcNow() => _zaman;
    }
}
