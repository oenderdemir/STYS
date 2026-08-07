using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.SatisBelgeleri;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Muhasebe.SatisBelgeleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using STYS.Muhasebe.SatisBelgeleri.Services;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// Faz 2B.10 görev md.8/md.9 - <see cref="EBelgeKurumPolitikaServisi.DegerlendirAsync"/>'in fail-closed
/// karar matrisini VE kurum bazlı tenant izolasyonunu, gerçek SQL Server'a karşı doğrular. Karar
/// sırası (global feature flag -> global cutover -> kurum politikası) BURADA gerçek
/// <see cref="EBelgeProcessingActivationGate"/> ile birlikte, AYRI bir algoritma yazılmadan test edilir.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SqlServerIntegrationCollection.Name)]
[Trait("Domain", "EBelge")]
[Trait("TestLevel", "SqlIntegration")]
[Trait("Dependency", "SqlServer")]
public class EBelgeKurumPolitikaServisiIntegrationTests : IAsyncLifetime
{
    private const string TestMarker = "EBF-KPS";

    private string _uniqueSuffix = TestMarker;
    private int _kurumId;
    private int _ilId;
    private int _tesisId;
    private int _kurumBId;
    private int _ilBId;
    private int _tesisBId;
    private int _musteriKartId;
    private string _suffixA = TestMarker;
    private string _suffixB = TestMarker;

    public async Task InitializeAsync()
    {
        _uniqueSuffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];
        _suffixA = _uniqueSuffix + "-A";
        _suffixB = _uniqueSuffix + "-B";

        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();

        // Faz 2B.10 - SeedKurumIlTesisAsync varsayılan olarak aktif bir test-only politika seed eder
        // (bkz. XML doc'u); bu dosyanın testleri KENDİ politika durumlarını KURDUĞU için o varsayılan
        // satır İLK ÖNCE silinir - her test kendi (yok/Yapilandirilmadi/Pasif/...) senaryosunu kurar.
        var (kurumA, ilA, tesisA) = await SatisBelgesiMuhasebeTestSupport.SeedKurumIlTesisAsync(dbContext, _suffixA);
        _kurumId = kurumA.Id;
        _ilId = ilA.Id;
        _tesisId = tesisA.Id;

        var (kurumB, ilB, tesisB) = await SatisBelgesiMuhasebeTestSupport.SeedKurumIlTesisAsync(dbContext, _suffixB);
        _kurumBId = kurumB.Id;
        _ilBId = ilB.Id;
        _tesisBId = tesisB.Id;

        await dbContext.Set<KurumEBelgePolitikasi>().IgnoreQueryFilters()
            .Where(p => p.KurumId == _kurumId || p.KurumId == _kurumBId)
            .ExecuteDeleteAsync();

        var musteriHesap = SatisBelgesiMuhasebeTestSupport.BuildHesap(_suffixA, "MUS", _tesisId);
        dbContext.MuhasebeHesapPlanlari.Add(musteriHesap);
        await dbContext.SaveChangesAsync();

        var musteriKart = SatisBelgesiMuhasebeTestSupport.BuildCariKart(_suffixA, "MUS", CariKartTipleri.Musteri, _tesisId, musteriHesap.Id);
        dbContext.CariKartlar.Add(musteriKart);
        await dbContext.SaveChangesAsync();
        _musteriKartId = musteriKart.Id;
    }

    /// <summary>Yalnız bir FK hedefi olarak gereken, gerçek (muhasebeleştirilmemiş) bir SatisBelgesi Id'si üretir.</summary>
    private async Task<int> CreateSatisBelgesiIdAsync(StysAppDbContext dbContext)
    {
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
        var request = new CreateSatisBelgesiRequest
        {
            BelgeNo = $"{_suffixA}-{Guid.NewGuid():N}"[..40],
            BelgeTipi = SatisBelgesiTipi.SatisFaturasi,
            TesisId = _tesisId,
            CariKartId = _musteriKartId,
            BelgeTarihi = new DateTime(2026, 7, 1),
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

        var created = await service.CreateAsync(request);
        return created.Id!.Value;
    }

    public async Task DisposeAsync()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        await SatisBelgesiMuhasebeTestSupport.CleanupAsync(dbContext, _uniqueSuffix + "-A", _tesisId, _kurumId, _ilId);
        await SatisBelgesiMuhasebeTestSupport.CleanupAsync(dbContext, _uniqueSuffix + "-B", _tesisBId, _kurumBId, _ilBId);
    }

    private static StysAppDbContext CreateDbContext() => SatisBelgesiMuhasebeTestSupport.CreateDbContext();

    /// <summary>Global kapı HER ZAMAN aktif (uzak geçmiş tarih) - kurum politikasının kendi davranışını izole test eder.</summary>
    private static IEBelgeKurumPolitikaServisi CreateServisiGlobalAktif(StysAppDbContext dbContext, TimeProvider? timeProvider = null)
    {
        var tp = timeProvider ?? TimeProvider.System;
        var gate = new EBelgeProcessingActivationGate(
            Options.Create(new EBelgeProcessingOptions { Enabled = true, NotBeforeLocalDate = "2020-01-01" }),
            tp,
            NullLogger<EBelgeProcessingActivationGate>.Instance);
        return new EBelgeKurumPolitikaServisi(dbContext, gate, new EBelgeYontemYetenekSaglayici(), tp);
    }

    private static IEBelgeKurumPolitikaServisi CreateServisiGlobalKapali(StysAppDbContext dbContext)
    {
        var gate = new EBelgeProcessingActivationGate(
            Options.Create(new EBelgeProcessingOptions { Enabled = false }),
            TimeProvider.System,
            NullLogger<EBelgeProcessingActivationGate>.Instance);
        return new EBelgeKurumPolitikaServisi(dbContext, gate, new EBelgeYontemYetenekSaglayici(), TimeProvider.System);
    }

    private static IEBelgeKurumPolitikaServisi CreateServisiGlobalHenuzGelmedi(StysAppDbContext dbContext)
    {
        var gate = new EBelgeProcessingActivationGate(
            Options.Create(new EBelgeProcessingOptions { Enabled = true, NotBeforeLocalDate = "2099-01-01" }),
            TimeProvider.System,
            NullLogger<EBelgeProcessingActivationGate>.Instance);
        return new EBelgeKurumPolitikaServisi(dbContext, gate, new EBelgeYontemYetenekSaglayici(), TimeProvider.System);
    }

    // md.3/md.28 - EN KRİTİK invariant: kurum politikası tam aktif/operasyonel olsa BİLE, global kapı
    // kapalıyken/henüz açılmamışken karar HER ZAMAN fail-closed'dır.
    [IntegrationFact]
    [Trait("CriticalInvariant", "InstitutionPolicyFailClosed")]
    public async Task KurumPolitikasiTamAktifOlsaBileGlobalKapaliyseKararIslemeIzinliDegildir()
    {
        await using var seedCtx = CreateDbContext();
        seedCtx.Add(new KurumEBelgePolitikasi
        {
            KurumId = _kurumId,
            EntegrasyonYontemi = EBelgeEntegrasyonYontemi.GibPortal,
            AktifMi = true,
            AktivasyonYerelTarihi = new DateTime(2020, 1, 1),
            PolitikaSurumu = 1,
        });
        await seedCtx.SaveChangesAsync();

        await using var dbContext = CreateDbContext();
        var kapaliServis = CreateServisiGlobalKapali(dbContext);
        var kapaliKarar = await kapaliServis.DegerlendirAsync(_kurumId, DateTime.Today);
        Assert.False(kapaliKarar.IslemeIzinliMi);
        Assert.Equal(EBelgeKurumPolitikaKararNedeni.GlobalKapali, kapaliKarar.Neden);

        await using var dbContext2 = CreateDbContext();
        var henuzServis = CreateServisiGlobalHenuzGelmedi(dbContext2);
        var henuzKarar = await henuzServis.DegerlendirAsync(_kurumId, DateTime.Today);
        Assert.False(henuzKarar.IslemeIzinliMi);
        Assert.Equal(EBelgeKurumPolitikaKararNedeni.GlobalAktivasyonTarihiGelmedi, henuzKarar.Neden);
    }

    [IntegrationFact]
    public async Task PolitikaSatiriHicYoksaPolitikaYapilandirilmadiDoner()
    {
        await using var dbContext = CreateDbContext();
        var servis = CreateServisiGlobalAktif(dbContext);

        var karar = await servis.DegerlendirAsync(_kurumId, DateTime.Today);

        Assert.False(karar.IslemeIzinliMi);
        Assert.Equal(EBelgeKurumPolitikaKararNedeni.PolitikaYapilandirilmadi, karar.Neden);
        Assert.Null(karar.PolitikaId);
    }

    [IntegrationFact]
    public async Task PolitikaAcikcaYapilandirilmadiIsePolitikaYapilandirilmadiDoner()
    {
        await using var seedCtx = CreateDbContext();
        seedCtx.Add(new KurumEBelgePolitikasi
        {
            KurumId = _kurumId,
            EntegrasyonYontemi = EBelgeEntegrasyonYontemi.Yapilandirilmadi,
            AktifMi = false,
            PolitikaSurumu = 1,
        });
        await seedCtx.SaveChangesAsync();

        await using var dbContext = CreateDbContext();
        var karar = await CreateServisiGlobalAktif(dbContext).DegerlendirAsync(_kurumId, DateTime.Today);

        Assert.False(karar.IslemeIzinliMi);
        Assert.Equal(EBelgeKurumPolitikaKararNedeni.PolitikaYapilandirilmadi, karar.Neden);
    }

    [IntegrationFact]
    public async Task PolitikaPasifIsePolitikaPasifDoner()
    {
        await using var seedCtx = CreateDbContext();
        seedCtx.Add(new KurumEBelgePolitikasi
        {
            KurumId = _kurumId,
            EntegrasyonYontemi = EBelgeEntegrasyonYontemi.GibPortal,
            AktifMi = false,
            AktivasyonYerelTarihi = new DateTime(2020, 1, 1),
            PolitikaSurumu = 1,
        });
        await seedCtx.SaveChangesAsync();

        await using var dbContext = CreateDbContext();
        var karar = await CreateServisiGlobalAktif(dbContext).DegerlendirAsync(_kurumId, DateTime.Today);

        Assert.False(karar.IslemeIzinliMi);
        Assert.Equal(EBelgeKurumPolitikaKararNedeni.PolitikaPasif, karar.Neden);
    }

    [IntegrationFact]
    public async Task KurumAktivasyonTarihiGelecektekiBirTarihseBelgeTarihiOncesindeReddedilir()
    {
        await using var seedCtx = CreateDbContext();
        seedCtx.Add(new KurumEBelgePolitikasi
        {
            KurumId = _kurumId,
            EntegrasyonYontemi = EBelgeEntegrasyonYontemi.GibPortal,
            AktifMi = true,
            AktivasyonYerelTarihi = new DateTime(2099, 1, 1),
            PolitikaSurumu = 1,
        });
        await seedCtx.SaveChangesAsync();

        await using var dbContext = CreateDbContext();
        var karar = await CreateServisiGlobalAktif(dbContext).DegerlendirAsync(_kurumId, DateTime.Today);

        Assert.False(karar.IslemeIzinliMi);
        Assert.Equal(EBelgeKurumPolitikaKararNedeni.KurumAktivasyonTarihiGelmedi, karar.Neden);
    }

    [IntegrationFact]
    public async Task AktivasyonYerelTarihiNullIkenAktifTrueVeriBozuklugunuGecersizOlarakIsaretler()
    {
        // Yönetim servisi bu durumu ASLA üretmez (bkz. ValidateHedefDurum) - burada doğrudan DB
        // manipülasyonuyla veri bütünlüğü ihlali simüle edilir; karar servisi yine de fail-closed olmalı.
        await using var seedCtx = CreateDbContext();
        seedCtx.Add(new KurumEBelgePolitikasi
        {
            KurumId = _kurumId,
            EntegrasyonYontemi = EBelgeEntegrasyonYontemi.GibPortal,
            AktifMi = true,
            AktivasyonYerelTarihi = null,
            PolitikaSurumu = 1,
        });
        await seedCtx.SaveChangesAsync();

        await using var dbContext = CreateDbContext();
        var karar = await CreateServisiGlobalAktif(dbContext).DegerlendirAsync(_kurumId, DateTime.Today);

        Assert.False(karar.IslemeIzinliMi);
        Assert.Equal(EBelgeKurumPolitikaKararNedeni.PolitikaGecersiz, karar.Neden);
    }

    [IntegrationFact]
    public async Task KullanilmayacakAcikBirHataDegildirAmaIslemeIzinliDegildir()
    {
        await using var seedCtx = CreateDbContext();
        seedCtx.Add(new KurumEBelgePolitikasi
        {
            KurumId = _kurumId,
            EntegrasyonYontemi = EBelgeEntegrasyonYontemi.Kullanilmayacak,
            AktifMi = true,
            AktivasyonYerelTarihi = new DateTime(2020, 1, 1),
            PolitikaSurumu = 1,
        });
        await seedCtx.SaveChangesAsync();

        await using var dbContext = CreateDbContext();
        var karar = await CreateServisiGlobalAktif(dbContext).DegerlendirAsync(_kurumId, DateTime.Today);

        Assert.False(karar.IslemeIzinliMi);
        Assert.Equal(EBelgeKurumPolitikaKararNedeni.YontemKullanilmayacak, karar.Neden);
        Assert.False(karar.Yetenekler.YerelSnapshotOlustur);
    }

    [IntegrationFact]
    public async Task HariciMuhasebeSistemiAcikBirHataDegildirAmaIslemeIzinliDegildir()
    {
        await using var seedCtx = CreateDbContext();
        seedCtx.Add(new KurumEBelgePolitikasi
        {
            KurumId = _kurumId,
            EntegrasyonYontemi = EBelgeEntegrasyonYontemi.HariciMuhasebeSistemi,
            AktifMi = true,
            AktivasyonYerelTarihi = new DateTime(2020, 1, 1),
            HariciSistemKodu = "ERP-1",
            PolitikaSurumu = 1,
        });
        await seedCtx.SaveChangesAsync();

        await using var dbContext = CreateDbContext();
        var karar = await CreateServisiGlobalAktif(dbContext).DegerlendirAsync(_kurumId, DateTime.Today);

        Assert.False(karar.IslemeIzinliMi);
        Assert.Equal(EBelgeKurumPolitikaKararNedeni.HariciSistemSorumlu, karar.Neden);
    }

    [IntegrationFact]
    public async Task GibPortalAktifIseIslemeIzinlidirVeYalnizSnapshotIleUnsignedUblYetenegiTasir()
    {
        await using var seedCtx = CreateDbContext();
        seedCtx.Add(new KurumEBelgePolitikasi
        {
            KurumId = _kurumId,
            EntegrasyonYontemi = EBelgeEntegrasyonYontemi.GibPortal,
            AktifMi = true,
            AktivasyonYerelTarihi = new DateTime(2020, 1, 1),
            PolitikaSurumu = 1,
        });
        await seedCtx.SaveChangesAsync();

        await using var dbContext = CreateDbContext();
        var karar = await CreateServisiGlobalAktif(dbContext).DegerlendirAsync(_kurumId, DateTime.Today);

        Assert.True(karar.IslemeIzinliMi);
        Assert.Equal(EBelgeKurumPolitikaKararNedeni.Aktif, karar.Neden);
        Assert.True(karar.Yetenekler.YerelSnapshotOlustur);
        Assert.True(karar.Yetenekler.YerelUnsignedUblOlustur);
        Assert.False(karar.Yetenekler.YerelImzaOlustur);
        Assert.False(karar.Yetenekler.OtomatikGonderimYap);
    }

    // md.9/md.27/md.28 - OzelEntegrator/DogrudanGib, DB'de elle AktifMi=true olarak işaretlense
    // BİLE (yönetim servisi bunu asla üretmez, ama karar servisi KENDİ BAŞINA da fail-closed olmalı)
    // gerçek bir adapter olmadan production yetenek matrisinde OperasyonelMi=false döner.
    [IntegrationFact]
    public async Task OzelEntegratorVeyaDogrudanGibElleAktifIsaretlenseBileDesteklenmezReddedilir()
    {
        await using var seedCtx = CreateDbContext();
        seedCtx.Add(new KurumEBelgePolitikasi
        {
            KurumId = _kurumId,
            EntegrasyonYontemi = EBelgeEntegrasyonYontemi.DogrudanGib,
            AktifMi = true,
            AktivasyonYerelTarihi = new DateTime(2020, 1, 1),
            PolitikaSurumu = 1,
        });
        await seedCtx.SaveChangesAsync();

        await using var dbContext = CreateDbContext();
        var karar = await CreateServisiGlobalAktif(dbContext).DegerlendirAsync(_kurumId, DateTime.Today);

        Assert.False(karar.IslemeIzinliMi);
        Assert.Equal(EBelgeKurumPolitikaKararNedeni.YontemHenuzDesteklenmiyor, karar.Neden);
        Assert.False(karar.Yetenekler.OperasyonelMi);
    }

    // md.20/md.28 - kurum A'nın politikası, kurum B'nin kararını HİÇBİR ŞEKİLDE etkilemez.
    [IntegrationFact]
    [Trait("CriticalInvariant", "InstitutionPolicyTenantIsolation")]
    public async Task KurumABurumPolitikasiKurumBKararinaSizmaz()
    {
        await using var seedCtx = CreateDbContext();
        seedCtx.Add(new KurumEBelgePolitikasi
        {
            KurumId = _kurumId,
            EntegrasyonYontemi = EBelgeEntegrasyonYontemi.GibPortal,
            AktifMi = true,
            AktivasyonYerelTarihi = new DateTime(2020, 1, 1),
            PolitikaSurumu = 1,
        });
        await seedCtx.SaveChangesAsync();
        // Kurum B İÇİN hiç politika seed edilmedi.

        await using var dbContext = CreateDbContext();
        var servis = CreateServisiGlobalAktif(dbContext);

        var kararA = await servis.DegerlendirAsync(_kurumId, DateTime.Today);
        var kararB = await servis.DegerlendirAsync(_kurumBId, DateTime.Today);

        Assert.True(kararA.IslemeIzinliMi);
        Assert.False(kararB.IslemeIzinliMi);
        Assert.Equal(EBelgeKurumPolitikaKararNedeni.PolitikaYapilandirilmadi, kararB.Neden);
    }

    // Faz 2B.10.1 görev md.3 - DegerlendirIslemUygunlugunuAsync: karar kaydı hiç yoksa (karar-öncesi/
    // legacy outbox mesajı) ARTIK fail-open "true" DEĞİL, açıkça fail-closed (`KararBulunamadi`).
    [IntegrationFact]
    [Trait("CriticalInvariant", "LegacyDecisionNeverProcesses")]
    public async Task DegerlendirIslemUygunlugunuAsyncKararKaydiYokIkenFailClosedDoner()
    {
        await using var dbContext = CreateDbContext();
        var servis = CreateServisiGlobalAktif(dbContext);

        var sonuc = await servis.DegerlendirIslemUygunlugunuAsync(_kurumId, eBelgeKaydiId: 999_999, EBelgeOutboxIsTuru.UblImzala);

        Assert.False(sonuc.UygunMu);
        Assert.Equal(EBelgeIslemPolitikaUygunlukNedeni.KararBulunamadi, sonuc.Neden);
    }

    // md.14 - karar ANINDA YerelImzaOlustur=true olsa bile GÜNCEL politika artık bunu desteklemiyorsa
    // (ör. GibPortal'a düşürüldüyse) işlem artık izinli DEĞİLDİR.
    [IntegrationFact]
    public async Task DegerlendirIslemUygunlugunuAsyncKararAnindaImzaIzinliAmaGuncelPolitikaArtikDesteklemiyorsaUygunDegilDoner()
    {
        await using var seedCtx = CreateDbContext();
        seedCtx.Add(new KurumEBelgePolitikasi
        {
            KurumId = _kurumId,
            EntegrasyonYontemi = EBelgeEntegrasyonYontemi.GibPortal, // YerelImzaOlustur=false
            AktifMi = true,
            AktivasyonYerelTarihi = new DateTime(2020, 1, 1),
            PolitikaSurumu = 1,
        });
        await seedCtx.SaveChangesAsync();

        var satisBelgesiId = await CreateSatisBelgesiIdAsync(seedCtx);
        var eBelgeKaydi = new EBelgeKaydi
        {
            KurumId = _kurumId,
            SatisBelgesiId = satisBelgesiId,
            EBelgeUuid = Guid.NewGuid().ToString("D"),
            EBelgeKanali = EBelgeKanali.EArsiv,
            Durum = EBelgeKaydiDurumu.SnapshotHazir,
        };
        seedCtx.EBelgeKayitlari.Add(eBelgeKaydi);
        await seedCtx.SaveChangesAsync();

        var karar = new SatisBelgesiEBelgeKarari
        {
            KurumId = _kurumId,
            SatisBelgesiId = satisBelgesiId,
            EntegrasyonYontemi = EBelgeEntegrasyonYontemi.DogrudanGib,
            KararNedeni = EBelgeKurumPolitikaKararNedeni.Aktif,
            YerelSnapshotOlustur = true,
            YerelUnsignedUblOlustur = true,
            YerelImzaOlustur = true, // karar ANINDA true idi
            KararZamaniUtc = DateTime.UtcNow,
            EBelgeKaydiId = eBelgeKaydi.Id,
        };
        seedCtx.Add(karar);
        await seedCtx.SaveChangesAsync();

        await using var dbContext = CreateDbContext();
        var servis = CreateServisiGlobalAktif(dbContext);

        var sonuc = await servis.DegerlendirIslemUygunlugunuAsync(_kurumId, eBelgeKaydi.Id, EBelgeOutboxIsTuru.UblImzala);

        // Karar DogrudanGib iken güncel politika GibPortal'a değişmiş - bu, "yöntem değişti"
        // durumudur (bkz. EBelgeIslemPolitikaUygunlukNedeni.YontemDegisti); her iki durumda da
        // sonuç fail-closed'dır.
        Assert.False(sonuc.UygunMu);
        Assert.Equal(EBelgeIslemPolitikaUygunlukNedeni.YontemDegisti, sonuc.Neden);
    }
}
