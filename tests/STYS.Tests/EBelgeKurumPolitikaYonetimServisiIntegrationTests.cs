using Microsoft.EntityFrameworkCore;
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
using TOD.Platform.SharedKernel.Exceptions;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// Faz 2B.10 görev md.15/md.16 - <see cref="EBelgeKurumPolitikaYonetimServisi"/>'nin optimistic
/// concurrency, pending-iş kilidi, kill-switch VE audit revizyon kurallarını gerçek SQL Server'a
/// karşı doğrular.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SqlServerIntegrationCollection.Name)]
[Trait("Domain", "EBelge")]
[Trait("TestLevel", "SqlIntegration")]
[Trait("Dependency", "SqlServer")]
public class EBelgeKurumPolitikaYonetimServisiIntegrationTests : IAsyncLifetime
{
    private const string TestMarker = "EBF-KYS";

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

        // Bu dosyanın testleri KENDİ politika durumlarını kurar - varsayılan test-only politika silinir.
        await dbContext.Set<KurumEBelgePolitikasi>().IgnoreQueryFilters()
            .Where(p => p.KurumId == _kurumId)
            .ExecuteDeleteAsync();

        var musteriHesap = SatisBelgesiMuhasebeTestSupport.BuildHesap(_uniqueSuffix, "MUS", _tesisId);
        dbContext.MuhasebeHesapPlanlari.Add(musteriHesap);
        await dbContext.SaveChangesAsync();

        var musteriKart = SatisBelgesiMuhasebeTestSupport.BuildCariKart(_uniqueSuffix, "MUS", CariKartTipleri.Musteri, _tesisId, musteriHesap.Id);
        dbContext.CariKartlar.Add(musteriKart);
        await dbContext.SaveChangesAsync();
        _musteriKartId = musteriKart.Id;
    }

    public async Task DisposeAsync()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        await SatisBelgesiMuhasebeTestSupport.CleanupAsync(dbContext, _uniqueSuffix, _tesisId, _kurumId, _ilId);
    }

    private static StysAppDbContext CreateDbContext() => SatisBelgesiMuhasebeTestSupport.CreateDbContext();

    /// <summary>Yalnız bir FK hedefi olarak gereken, gerçek (muhasebeleştirilmemiş) bir SatisBelgesi Id'si üretir.</summary>
    private async Task<int> CreateSatisBelgesiIdAsync(StysAppDbContext dbContext)
    {
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
        var request = new CreateSatisBelgesiRequest
        {
            BelgeNo = $"{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
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

    private static IEBelgeKurumPolitikaYonetimServisi CreateServis(StysAppDbContext dbContext, TimeProvider? timeProvider = null, string notBeforeLocalDate = "2020-01-01") =>
        new EBelgeKurumPolitikaYonetimServisi(
            dbContext,
            new EBelgeYontemYetenekSaglayici(),
            Options.Create(new EBelgeProcessingOptions { Enabled = true, NotBeforeLocalDate = notBeforeLocalDate }),
            timeProvider ?? TimeProvider.System);

    private static KurumEBelgePolitikasiGuncellemeDto DtoIcin(
        EBelgeEntegrasyonYontemi yontem, bool aktifMi, DateTime? aktivasyon, string? hariciKod, string rowVersion, string neden = "test") => new()
    {
        EntegrasyonYontemi = yontem,
        AktifMi = aktifMi,
        AktivasyonYerelTarihi = aktivasyon,
        HariciSistemKodu = hariciKod,
        DegisiklikNedeni = neden,
        RowVersion = rowVersion,
    };

    // ---- İlk oluşturma ----

    [IntegrationFact]
    public async Task IlkOlusturmaBosRowVersionIleYapilirVeSurum1Olur()
    {
        await using var dbContext = CreateDbContext();
        var servis = CreateServis(dbContext);

        var sonuc = await servis.GuncelleAsync(_kurumId, DtoIcin(
            EBelgeEntegrasyonYontemi.GibPortal, true, new DateTime(2020, 1, 1), null, ""));

        Assert.Equal(1, sonuc.PolitikaSurumu);
        Assert.True(sonuc.AktifMi);
        Assert.Equal(EBelgeEntegrasyonYontemi.GibPortal, sonuc.EntegrasyonYontemi);
    }

    [IntegrationFact]
    public async Task IlkOlusturmadaDoluRowVersionVerilirseConcurrencyIhlaliOlarakReddedilir()
    {
        await using var dbContext = CreateDbContext();
        var servis = CreateServis(dbContext);

        await Assert.ThrowsAsync<EBelgeKurumPolitikaConcurrencyException>(() => servis.GuncelleAsync(_kurumId, DtoIcin(
            EBelgeEntegrasyonYontemi.GibPortal, true, new DateTime(2020, 1, 1), null, Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8]))));
    }

    [IntegrationFact]
    public async Task IlkOlusturmadaAuditRevizyonuEskiSurum0YeniSurum1IleYazilir()
    {
        await using var dbContext = CreateDbContext();
        var servis = CreateServis(dbContext);

        var politika = await servis.GuncelleAsync(_kurumId, DtoIcin(
            EBelgeEntegrasyonYontemi.GibPortal, true, new DateTime(2020, 1, 1), null, ""));

        var revizyonlar = await servis.RevizyonlariGetirAsync(_kurumId);
        var revizyon = Assert.Single(revizyonlar);
        Assert.Equal(0, revizyon.EskiSurum);
        Assert.Equal(1, revizyon.YeniSurum);
        Assert.Equal(EBelgeEntegrasyonYontemi.Yapilandirilmadi, revizyon.EskiYontem);
        Assert.Equal(EBelgeEntegrasyonYontemi.GibPortal, revizyon.YeniYontem);
        Assert.False(revizyon.EskiAktifMi);
        Assert.True(revizyon.YeniAktifMi);
        Assert.Equal(politika.Id, revizyon.KurumEBelgePolitikasiId);
    }

    // ---- Doğrulama kuralları (Pasif -> Aktif) ----

    [IntegrationFact]
    public async Task AktivasyonTarihiEksikIkenAktifHedeflenirseReddedilir()
    {
        await using var dbContext = CreateDbContext();
        var servis = CreateServis(dbContext);

        await Assert.ThrowsAsync<BaseException>(() => servis.GuncelleAsync(_kurumId, DtoIcin(
            EBelgeEntegrasyonYontemi.GibPortal, true, null, null, "")));
    }

    [IntegrationFact]
    public async Task AktivasyonTarihiGlobalTarihtenOnceyseReddedilir()
    {
        await using var dbContext = CreateDbContext();
        var servis = CreateServis(dbContext, notBeforeLocalDate: "2026-09-15");

        await Assert.ThrowsAsync<BaseException>(() => servis.GuncelleAsync(_kurumId, DtoIcin(
            EBelgeEntegrasyonYontemi.GibPortal, true, new DateTime(2026, 9, 14), null, "")));
    }

    [IntegrationFact]
    public async Task HariciMuhasebeSistemiIcinHariciSistemKoduEksikseReddedilir()
    {
        await using var dbContext = CreateDbContext();
        var servis = CreateServis(dbContext);

        await Assert.ThrowsAsync<BaseException>(() => servis.GuncelleAsync(_kurumId, DtoIcin(
            EBelgeEntegrasyonYontemi.HariciMuhasebeSistemi, true, new DateTime(2020, 1, 1), null, "")));
    }

    [Theory]
    [InlineData(EBelgeEntegrasyonYontemi.Kullanilmayacak)]
    [InlineData(EBelgeEntegrasyonYontemi.GibPortal)]
    public async Task KullanilmayacakVeyaGibPortalIcinHariciSistemKoduDoluysaReddedilir(EBelgeEntegrasyonYontemi yontem)
    {
        await using var dbContext = CreateDbContext();
        var servis = CreateServis(dbContext);

        await Assert.ThrowsAsync<BaseException>(() => servis.GuncelleAsync(_kurumId, DtoIcin(
            yontem, true, new DateTime(2020, 1, 1), "GEREKSIZ-KOD", "")));
    }

    [Theory]
    [InlineData(EBelgeEntegrasyonYontemi.OzelEntegrator)]
    [InlineData(EBelgeEntegrasyonYontemi.DogrudanGib)]
    public async Task GercekAdapterOlmayanYontemlerAktifOlarakAktiveEdilemez(EBelgeEntegrasyonYontemi yontem)
    {
        await using var dbContext = CreateDbContext();
        var servis = CreateServis(dbContext);

        var ex = await Assert.ThrowsAsync<EBelgeKurumPolitikaEngelliException>(() => servis.GuncelleAsync(_kurumId, DtoIcin(
            yontem, true, new DateTime(2020, 1, 1), null, "")));

        Assert.Equal(EBelgeKurumPolitikaKararNedeni.YontemHenuzDesteklenmiyor, ex.Neden);
    }

    [IntegrationFact]
    public async Task DegisiklikNedeniBossaReddedilir()
    {
        await using var dbContext = CreateDbContext();
        var servis = CreateServis(dbContext);

        await Assert.ThrowsAsync<BaseException>(() => servis.GuncelleAsync(_kurumId, DtoIcin(
            EBelgeEntegrasyonYontemi.GibPortal, true, new DateTime(2020, 1, 1), null, "", neden: "  ")));
    }

    // ---- Pasif hedeflenirken doğrulama uygulanmaz (ilk oluşturmada dahi) ----

    [IntegrationFact]
    public async Task PasifHedeflenirkenAktivasyonTarihiVeYontemKurallariUygulanmaz()
    {
        await using var dbContext = CreateDbContext();
        var servis = CreateServis(dbContext);

        var sonuc = await servis.GuncelleAsync(_kurumId, DtoIcin(
            EBelgeEntegrasyonYontemi.OzelEntegrator, false, null, null, ""));

        Assert.False(sonuc.AktifMi);
        Assert.Equal(EBelgeEntegrasyonYontemi.OzelEntegrator, sonuc.EntegrasyonYontemi);
    }

    // ---- Optimistic concurrency ----

    [IntegrationFact]
    public async Task EskiRowVersionIleGuncellemeConcurrencyIhlaliVerir()
    {
        await using var dbContext = CreateDbContext();
        var servis = CreateServis(dbContext);
        var ilk = await servis.GuncelleAsync(_kurumId, DtoIcin(
            EBelgeEntegrasyonYontemi.GibPortal, true, new DateTime(2020, 1, 1), null, ""));

        // Aynı satırı BAŞKA bir çağrı GÜNCEL RowVersion ile değiştirir - ilk (artık ESKİ) RowVersion'la tekrar deneme.
        await using var dbContext2 = CreateDbContext();
        var servis2 = CreateServis(dbContext2);
        await servis2.GuncelleAsync(_kurumId, DtoIcin(
            EBelgeEntegrasyonYontemi.GibPortal, false, null, null, Convert.ToBase64String(ilk.RowVersion)));

        await using var dbContext3 = CreateDbContext();
        var servis3 = CreateServis(dbContext3);
        await Assert.ThrowsAsync<EBelgeKurumPolitikaConcurrencyException>(() => servis3.GuncelleAsync(_kurumId, DtoIcin(
            EBelgeEntegrasyonYontemi.GibPortal, true, new DateTime(2020, 1, 1), null, Convert.ToBase64String(ilk.RowVersion))));
    }

    // ---- Kill switch: Aktif -> Pasif her zaman izinli, pending iş varken bile ----

    [IntegrationFact]
    public async Task AktifTenPasifeGecisPendingIsVarkenBileHerZamanIzinlidir()
    {
        await using var seedCtx = CreateDbContext();
        var servis = CreateServis(seedCtx);
        var ilk = await servis.GuncelleAsync(_kurumId, DtoIcin(
            EBelgeEntegrasyonYontemi.GibPortal, true, new DateTime(2020, 1, 1), null, ""));

        // Pending iş: SnapshotHazir durumunda bir EBelgeKaydi.
        var satisBelgesiId = await CreateSatisBelgesiIdAsync(seedCtx);
        seedCtx.EBelgeKayitlari.Add(new EBelgeKaydi
        {
            KurumId = _kurumId,
            SatisBelgesiId = satisBelgesiId,
            EBelgeUuid = Guid.NewGuid().ToString("D"),
            EBelgeKanali = EBelgeKanali.EArsiv,
            Durum = EBelgeKaydiDurumu.SnapshotHazir,
        });
        await seedCtx.SaveChangesAsync();

        await using var dbContext = CreateDbContext();
        var servis2 = CreateServis(dbContext);

        // Yöntem AYNI kalır, yalnız AktifMi=false hedeflenir - bu bir "sadece deaktivasyon"dur ve
        // pending iş kontrolüne TABİ DEĞİLDİR (bkz. görev md.15).
        var sonuc = await servis2.GuncelleAsync(_kurumId, DtoIcin(
            EBelgeEntegrasyonYontemi.GibPortal, false, new DateTime(2020, 1, 1), null, Convert.ToBase64String(ilk.RowVersion)));

        Assert.False(sonuc.AktifMi);
    }

    /// <summary>
    /// Faz 2B.10.2 görev md.9/md.10 - "worker'ın politika kilidi ÖNCE kazandığı" sıralamanın
    /// GERÇEK kanıtı: worker'ı TEMSİL EDEN bir bağlantı, <see cref="IEBelgeKurumPolitikaTransactionGuard"/>
    /// ile politika satırını KİLİTLER (kendi transaction'ı İÇİNDE, HENÜZ commit ETMEDEN) - bu SIRADA
    /// başlatılan GERÇEK bir kill switch `GuncelleAsync` çağrısı (AYRI bir bağlantı/transaction),
    /// UPDATE aşamasında (SELECT'i DEĞİL - SELECT bir S kilidi ister, U kilidiyle UYUMLUDUR; ama
    /// UPDATE bir X kilidi ister, U kilidiyle ÇAKIŞIR) worker'ın kilidine ÇARPARAK GERÇEKTEN BLOKE
    /// OLUR. Bu, task'ın makul bir süre TAMAMLANMADIĞI doğrulanarak KANITLANIR. Worker'ın
    /// transaction'ı COMMIT edildiğinde (satırı DEĞİŞTİRMEDEN) kill switch serbest kalır ve normal
    /// şekilde tamamlanır - "worker'ın kilidi ÖNCE kazanması → worker commit → SONRA kill switch
    /// commit" sıralamasının (görev md.9) GERÇEK bir kanıtıdır.
    /// </summary>
    [IntegrationFact]
    [Trait("CriticalInvariant", "PolicyKillSwitchPreventsCommit")]
    public async Task WorkerPolitikaKilidiOnceAlinirsaKillSwitchGuncellemesiKilitSerbestKalanaKadarGercektenBlokeOlur()
    {
        await using var seedCtx = CreateDbContext();
        var servis = CreateServis(seedCtx);
        var ilk = await servis.GuncelleAsync(_kurumId, DtoIcin(
            EBelgeEntegrasyonYontemi.GibPortal, true, new DateTime(2020, 1, 1), null, ""));

        await using var workerCtx = CreateDbContext();
        await using var workerTx = await workerCtx.Database.BeginTransactionAsync();
        var guard = new EBelgeKurumPolitikaTransactionGuard(workerCtx);
        var kilitliSnapshot = await guard.KilitleVeOkuAsync(_kurumId);
        Assert.NotNull(kilitliSnapshot);
        Assert.True(kilitliSnapshot!.AktifMi);

        await using var adminCtx = CreateDbContext();
        var adminServis = CreateServis(adminCtx);
        var killSwitchTask = Task.Run(() => adminServis.GuncelleAsync(_kurumId, DtoIcin(
            EBelgeEntegrasyonYontemi.GibPortal, false, new DateTime(2020, 1, 1), null, Convert.ToBase64String(ilk.RowVersion))));

        var erkenTamamlandiMi = await Task.WhenAny(killSwitchTask, Task.Delay(TimeSpan.FromSeconds(2))) == killSwitchTask;
        Assert.False(erkenTamamlandiMi, "kill switch GuncelleAsync'i, worker'ın TUTTUĞU politika satırı kilidine ÇARPMADI - test GERÇEK bir kilit çakışması KURAMADI.");

        await workerTx.CommitAsync();

        var sonuc = await killSwitchTask;
        Assert.False(sonuc.AktifMi);
    }

    /// <summary>
    /// Faz 2B.10.3 görev md.13 - <see cref="IEBelgeKurumPolitikaTransactionGuard.KilitleVeOkuAsync"/>
    /// AÇIK bir ambient transaction OLMADAN çağrılırsa fail-FAST bir <see cref="InvalidOperationException"/>
    /// fırlatmalıdır - `HOLDLOCK`'un verdiği "transaction sonuna kadar tutulan kilit" garantisi,
    /// tutacak bir transaction YOKSA sessizce KAYBOLUR; bu, business bir fallback'e ASLA ÇEVRİLMEZ.
    /// </summary>
    [IntegrationFact]
    public async Task TransactionGuardAcikTransactionOlmadanCagrilirsaFailFastExceptionFirlatir()
    {
        await using var seedCtx = CreateDbContext();
        var servis = CreateServis(seedCtx);
        await servis.GuncelleAsync(_kurumId, DtoIcin(
            EBelgeEntegrasyonYontemi.GibPortal, true, new DateTime(2020, 1, 1), null, ""));

        await using var dbContext = CreateDbContext();
        var guard = new EBelgeKurumPolitikaTransactionGuard(dbContext);

        // KASITLI olarak `Database.BeginTransactionAsync()` HİÇ çağrılmadı.
        await Assert.ThrowsAsync<InvalidOperationException>(() => guard.KilitleVeOkuAsync(_kurumId));
    }

    // ---- Pending iş varken YÖNTEM değişimi engellenir ----

    [IntegrationFact]
    public async Task PendingIsVarkenYontemDegisimiEngellenir()
    {
        await using var seedCtx = CreateDbContext();
        var servis = CreateServis(seedCtx);
        var ilk = await servis.GuncelleAsync(_kurumId, DtoIcin(
            EBelgeEntegrasyonYontemi.GibPortal, true, new DateTime(2020, 1, 1), null, ""));

        var satisBelgesiId = await CreateSatisBelgesiIdAsync(seedCtx);
        seedCtx.EBelgeKayitlari.Add(new EBelgeKaydi
        {
            KurumId = _kurumId,
            SatisBelgesiId = satisBelgesiId,
            EBelgeUuid = Guid.NewGuid().ToString("D"),
            EBelgeKanali = EBelgeKanali.EArsiv,
            Durum = EBelgeKaydiDurumu.UnsignedUblHazir,
        });
        await seedCtx.SaveChangesAsync();

        await using var dbContext = CreateDbContext();
        var servis2 = CreateServis(dbContext);

        // Yöntem GibPortal -> Kullanilmayacak DEĞİŞİYOR - pending iş bunu engellemelidir.
        await Assert.ThrowsAsync<EBelgeKurumPolitikaDegisiklikEngellendiException>(() => servis2.GuncelleAsync(_kurumId, DtoIcin(
            EBelgeEntegrasyonYontemi.Kullanilmayacak, true, new DateTime(2020, 1, 1), null, Convert.ToBase64String(ilk.RowVersion))));
    }

    [IntegrationFact]
    public async Task PendingIsYokkenYontemDegisimiSerbesttir()
    {
        await using var dbContext = CreateDbContext();
        var servis = CreateServis(dbContext);
        var ilk = await servis.GuncelleAsync(_kurumId, DtoIcin(
            EBelgeEntegrasyonYontemi.GibPortal, true, new DateTime(2020, 1, 1), null, ""));

        var sonuc = await servis.GuncelleAsync(_kurumId, DtoIcin(
            EBelgeEntegrasyonYontemi.Kullanilmayacak, true, new DateTime(2020, 1, 1), null, Convert.ToBase64String(ilk.RowVersion)));

        Assert.Equal(EBelgeEntegrasyonYontemi.Kullanilmayacak, sonuc.EntegrasyonYontemi);
        Assert.Equal(2, sonuc.PolitikaSurumu);
    }

    [IntegrationFact]
    public async Task GetirAsyncKayitYoksaNullDoner()
    {
        await using var dbContext = CreateDbContext();
        var servis = CreateServis(dbContext);

        var sonuc = await servis.GetirAsync(_kurumId);

        Assert.Null(sonuc);
    }
}
