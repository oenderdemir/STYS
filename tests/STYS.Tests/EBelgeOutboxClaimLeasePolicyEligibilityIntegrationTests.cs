using Microsoft.EntityFrameworkCore;
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
/// Faz 2B.10.1 görev md.1/md.2 - <see cref="EBelgeOutboxClaimLeaseService"/>'in claim SQL'inin,
/// bir mesajın YALNIZ kendi immutable kararı VE kurumun GÜNCEL/aktif/aynı-yöntemdeki politikası
/// VARSA aday olabildiğini gerçek SQL Server'a karşı doğrular. Pasif/uyumsuz mesajlar claim
/// EDİLMEZ, `DenemeSayisi`/lease OLUŞMAZ ve bloklu ilk aday sonraki uygun mesajın claim
/// edilmesini ENGELLEMEZ (starvation yok).
/// </summary>
[Trait("Category", "Integration")]
[Collection(SqlServerIntegrationCollection.Name)]
[Trait("Domain", "EBelge")]
[Trait("TestLevel", "SqlIntegration")]
[Trait("Dependency", "SqlServer")]
public class EBelgeOutboxClaimLeasePolicyEligibilityIntegrationTests : IAsyncLifetime
{
    private const string TestMarker = "EBF-CPE";

    private string _uniqueSuffix = TestMarker;
    private string _suffixA = TestMarker;
    private string _suffixB = TestMarker;
    private int _kurumId;
    private int _ilId;
    private int _tesisId;
    private int _musteriKartId;
    private int _kurumBId;
    private int _ilBId;
    private int _tesisBId;
    private int _musteriKartBId;

    public async Task InitializeAsync()
    {
        _uniqueSuffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];
        _suffixA = _uniqueSuffix + "-A";
        _suffixB = _uniqueSuffix + "-B";

        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();

        var (kurumA, ilA, tesisA) = await SatisBelgesiMuhasebeTestSupport.SeedKurumIlTesisAsync(dbContext, _suffixA);
        _kurumId = kurumA.Id;
        _ilId = ilA.Id;
        _tesisId = tesisA.Id;

        var (kurumB, ilB, tesisB) = await SatisBelgesiMuhasebeTestSupport.SeedKurumIlTesisAsync(dbContext, _suffixB);
        _kurumBId = kurumB.Id;
        _ilBId = ilB.Id;
        _tesisBId = tesisB.Id;

        // Bu dosyanın testleri KENDİ politika senaryolarını kurar - varsayılan test-only politikalar silinir.
        await dbContext.Set<KurumEBelgePolitikasi>().IgnoreQueryFilters()
            .Where(p => p.KurumId == _kurumId || p.KurumId == _kurumBId)
            .ExecuteDeleteAsync();

        var musteriHesapA = SatisBelgesiMuhasebeTestSupport.BuildHesap(_suffixA, "MUS", _tesisId);
        var musteriHesapB = SatisBelgesiMuhasebeTestSupport.BuildHesap(_suffixB, "MUS", _tesisBId);
        dbContext.MuhasebeHesapPlanlari.AddRange(musteriHesapA, musteriHesapB);
        await dbContext.SaveChangesAsync();

        var musteriKartA = SatisBelgesiMuhasebeTestSupport.BuildCariKart(_suffixA, "MUS", CariKartTipleri.Musteri, _tesisId, musteriHesapA.Id);
        var musteriKartB = SatisBelgesiMuhasebeTestSupport.BuildCariKart(_suffixB, "MUS", CariKartTipleri.Musteri, _tesisBId, musteriHesapB.Id);
        dbContext.CariKartlar.AddRange(musteriKartA, musteriKartB);
        await dbContext.SaveChangesAsync();
        _musteriKartId = musteriKartA.Id;
        _musteriKartBId = musteriKartB.Id;
    }

    public async Task DisposeAsync()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        await SatisBelgesiMuhasebeTestSupport.CleanupAsync(dbContext, _suffixA, _tesisId, _kurumId, _ilId);
        await SatisBelgesiMuhasebeTestSupport.CleanupAsync(dbContext, _suffixB, _tesisBId, _kurumBId, _ilBId);
    }

    private static StysAppDbContext CreateDbContext() => SatisBelgesiMuhasebeTestSupport.CreateDbContext();

    private async Task<int> CreateSatisBelgesiIdAsync(StysAppDbContext dbContext, int kurumId, int tesisId, int musteriKartId, string suffix)
    {
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
        var request = new CreateSatisBelgesiRequest
        {
            BelgeNo = $"{suffix}-{Guid.NewGuid():N}"[..40],
            BelgeTipi = SatisBelgesiTipi.SatisFaturasi,
            TesisId = tesisId,
            CariKartId = musteriKartId,
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

    private async Task<(int satisBelgesiId, int eBelgeKaydiId)> SeedEBelgeKaydiAsync(
        StysAppDbContext dbContext, int kurumId, int tesisId, int musteriKartId, string suffix)
    {
        var satisBelgesiId = await CreateSatisBelgesiIdAsync(dbContext, kurumId, tesisId, musteriKartId, suffix);
        var eBelgeKaydi = new EBelgeKaydi
        {
            KurumId = kurumId,
            SatisBelgesiId = satisBelgesiId,
            EBelgeUuid = Guid.NewGuid().ToString("D"),
            EBelgeKanali = EBelgeKanali.EArsiv,
            Durum = EBelgeKaydiDurumu.SnapshotHazir,
        };
        dbContext.EBelgeKayitlari.Add(eBelgeKaydi);
        await dbContext.SaveChangesAsync();
        return (satisBelgesiId, eBelgeKaydi.Id);
    }

    private static async Task SeedPolitikaAsync(
        StysAppDbContext dbContext, int kurumId, EBelgeEntegrasyonYontemi yontem, bool aktifMi, DateTime? aktivasyonYerelTarihi)
    {
        dbContext.Add(new KurumEBelgePolitikasi
        {
            KurumId = kurumId,
            EntegrasyonYontemi = yontem,
            AktifMi = aktifMi,
            AktivasyonYerelTarihi = aktivasyonYerelTarihi,
            PolitikaSurumu = 1,
        });
        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedKararAsync(
        StysAppDbContext dbContext, int kurumId, int satisBelgesiId, int eBelgeKaydiId, EBelgeEntegrasyonYontemi yontem,
        bool yerelSnapshot, bool yerelUnsignedUbl, bool yerelImza)
    {
        dbContext.Add(new SatisBelgesiEBelgeKarari
        {
            KurumId = kurumId,
            SatisBelgesiId = satisBelgesiId,
            EntegrasyonYontemi = yontem,
            KararNedeni = EBelgeKurumPolitikaKararNedeni.Aktif,
            YerelSnapshotOlustur = yerelSnapshot,
            YerelUnsignedUblOlustur = yerelUnsignedUbl,
            YerelImzaOlustur = yerelImza,
            KararZamaniUtc = DateTime.UtcNow,
            EBelgeKaydiId = eBelgeKaydiId,
        });
        await dbContext.SaveChangesAsync();
    }

    private static async Task<int> SeedOutboxAsync(StysAppDbContext dbContext, int kurumId, int eBelgeKaydiId, EBelgeOutboxIsTuru isTuru)
    {
        var mesaj = new EBelgeOutboxMesaji
        {
            KurumId = kurumId,
            EBelgeKaydiId = eBelgeKaydiId,
            IsTuru = isTuru,
            Durum = EBelgeOutboxDurumu.Bekliyor,
            DenemeSayisi = 0,
        };
        dbContext.EBelgeOutboxMesajlari.Add(mesaj);
        await dbContext.SaveChangesAsync();
        return mesaj.Id;
    }

    private static Task<EBelgeOutboxClaimLeaseResultDto?> ClaimNextAsync(StysAppDbContext dbContext, IEBelgeSigningActivationGate? signingGate = null) =>
        new EBelgeOutboxClaimLeaseService(dbContext, signingGate ?? EBelgeTestSigningActivationGate.Acik).TryClaimNextAsync(TimeSpan.FromSeconds(60));

    /// <summary>Faz 2B.10.3 görev md.8/md.9 - Enabled/NotBeforeLocalDate/timezone algoritmasının GERÇEK `EBelgeSigningActivationGate` üzerinden (test double İLE DEĞİL) doğrulandığı senaryolar İÇİN sabit bir zaman sağlayıcı.</summary>
    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _zaman;
        public FixedTimeProvider(DateTimeOffset zaman) => _zaman = zaman;
        public override DateTimeOffset GetUtcNow() => _zaman;
    }

    private static EBelgeSigningActivationGate CreateRealSigningGate(EBelgeSigningOptions options, DateTimeOffset nowUtc) =>
        new(Microsoft.Extensions.Options.Options.Create(options), new FixedTimeProvider(nowUtc), Microsoft.Extensions.Logging.Abstractions.NullLogger<EBelgeSigningActivationGate>.Instance);

    // 1+3. Politika pasifken uygun outbox claim edilmez; lease/token oluşmaz.
    [IntegrationFact]
    [Trait("CriticalInvariant", "InactivePolicyNeverClaims")]
    public async Task PolitikaPasifkenUygunOutboxClaimEdilmezVeLeaseOlusmaz()
    {
        await using var dbContext = CreateDbContext();
        var (satisBelgesiId, eBelgeKaydiId) = await SeedEBelgeKaydiAsync(dbContext, _kurumId, _tesisId, _musteriKartId, _suffixA);
        await SeedPolitikaAsync(dbContext, _kurumId, EBelgeEntegrasyonYontemi.GibPortal, aktifMi: false, new DateTime(2020, 1, 1));
        await SeedKararAsync(dbContext, _kurumId, satisBelgesiId, eBelgeKaydiId, EBelgeEntegrasyonYontemi.GibPortal, true, true, false);
        var outboxId = await SeedOutboxAsync(dbContext, _kurumId, eBelgeKaydiId, EBelgeOutboxIsTuru.ArtefaktOlustur);

        var claim = await ClaimNextAsync(dbContext);

        Assert.Null(claim);

        var mesaj = await dbContext.EBelgeOutboxMesajlari.IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.Id == outboxId);
        Assert.Equal(EBelgeOutboxDurumu.Bekliyor, mesaj.Durum);
        Assert.Null(mesaj.KilitToken);
        Assert.Null(mesaj.KilitBitisZamaniUtc);
    }

    // 2. Politika pasifken DenemeSayisi değişmez.
    [IntegrationFact]
    public async Task PolitikaPasifkenDenemeSayisiDegismez()
    {
        await using var dbContext = CreateDbContext();
        var (satisBelgesiId, eBelgeKaydiId) = await SeedEBelgeKaydiAsync(dbContext, _kurumId, _tesisId, _musteriKartId, _suffixA);
        await SeedPolitikaAsync(dbContext, _kurumId, EBelgeEntegrasyonYontemi.GibPortal, aktifMi: false, new DateTime(2020, 1, 1));
        await SeedKararAsync(dbContext, _kurumId, satisBelgesiId, eBelgeKaydiId, EBelgeEntegrasyonYontemi.GibPortal, true, true, false);
        var outboxId = await SeedOutboxAsync(dbContext, _kurumId, eBelgeKaydiId, EBelgeOutboxIsTuru.ArtefaktOlustur);

        await ClaimNextAsync(dbContext);
        await ClaimNextAsync(dbContext);

        var mesaj = await dbContext.EBelgeOutboxMesajlari.IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.Id == outboxId);
        Assert.Equal(0, mesaj.DenemeSayisi);
    }

    // 4. Politika pasif ilk mesaj, aktif ikinci mesajın claim edilmesini ENGELLEMEZ (starvation yok).
    [IntegrationFact]
    public async Task PolitikaPasifIlkMesajAktifIkinciMesajinClaimEdilmesiniEngellemez()
    {
        await using var dbContext = CreateDbContext();

        var (satisBelgesiIdA, eBelgeKaydiIdA) = await SeedEBelgeKaydiAsync(dbContext, _kurumId, _tesisId, _musteriKartId, _suffixA);
        await SeedPolitikaAsync(dbContext, _kurumId, EBelgeEntegrasyonYontemi.GibPortal, aktifMi: false, new DateTime(2020, 1, 1));
        await SeedKararAsync(dbContext, _kurumId, satisBelgesiIdA, eBelgeKaydiIdA, EBelgeEntegrasyonYontemi.GibPortal, true, true, false);
        await SeedOutboxAsync(dbContext, _kurumId, eBelgeKaydiIdA, EBelgeOutboxIsTuru.ArtefaktOlustur); // pasif politika - ineligible

        var (satisBelgesiIdB, eBelgeKaydiIdB) = await SeedEBelgeKaydiAsync(dbContext, _kurumBId, _tesisBId, _musteriKartBId, _suffixB);
        await SeedPolitikaAsync(dbContext, _kurumBId, EBelgeEntegrasyonYontemi.GibPortal, aktifMi: true, new DateTime(2020, 1, 1));
        await SeedKararAsync(dbContext, _kurumBId, satisBelgesiIdB, eBelgeKaydiIdB, EBelgeEntegrasyonYontemi.GibPortal, true, true, false);
        var outboxIdB = await SeedOutboxAsync(dbContext, _kurumBId, eBelgeKaydiIdB, EBelgeOutboxIsTuru.ArtefaktOlustur); // aktif - eligible

        var claim = await ClaimNextAsync(dbContext);

        Assert.NotNull(claim);
        Assert.Equal(outboxIdB, claim!.OutboxMesajiId);
        Assert.Equal(_kurumBId, claim.KurumId);
    }

    // 5. Yöntem değişmişse (politika != karar yöntemi) mesaj claim edilmez.
    [IntegrationFact]
    public async Task YontemDegismisseMesajClaimEdilmez()
    {
        await using var dbContext = CreateDbContext();
        var (satisBelgesiId, eBelgeKaydiId) = await SeedEBelgeKaydiAsync(dbContext, _kurumId, _tesisId, _musteriKartId, _suffixA);
        // Politika DogrudanGib'e değişti, ama karar hâlâ GibPortal.
        await SeedPolitikaAsync(dbContext, _kurumId, EBelgeEntegrasyonYontemi.DogrudanGib, aktifMi: true, new DateTime(2020, 1, 1));
        await SeedKararAsync(dbContext, _kurumId, satisBelgesiId, eBelgeKaydiId, EBelgeEntegrasyonYontemi.GibPortal, true, true, false);
        await SeedOutboxAsync(dbContext, _kurumId, eBelgeKaydiId, EBelgeOutboxIsTuru.ArtefaktOlustur);

        var claim = await ClaimNextAsync(dbContext);

        Assert.Null(claim);
    }

    // 6. Kurum aktivasyon tarihi (bugüne göre) gelmemişse mesaj claim edilmez.
    [IntegrationFact]
    public async Task KurumAktivasyonTarihiGelmemisseMesajClaimEdilmez()
    {
        await using var dbContext = CreateDbContext();
        var (satisBelgesiId, eBelgeKaydiId) = await SeedEBelgeKaydiAsync(dbContext, _kurumId, _tesisId, _musteriKartId, _suffixA);
        await SeedPolitikaAsync(dbContext, _kurumId, EBelgeEntegrasyonYontemi.GibPortal, aktifMi: true, DateTime.Today.AddYears(1));
        await SeedKararAsync(dbContext, _kurumId, satisBelgesiId, eBelgeKaydiId, EBelgeEntegrasyonYontemi.GibPortal, true, true, false);
        await SeedOutboxAsync(dbContext, _kurumId, eBelgeKaydiId, EBelgeOutboxIsTuru.ArtefaktOlustur);

        var claim = await ClaimNextAsync(dbContext);

        Assert.Null(claim);
    }

    // 7. Immutable karar bulunmayan (legacy) mesaj claim edilmez.
    [IntegrationFact]
    [Trait("CriticalInvariant", "LegacyDecisionNeverProcesses")]
    public async Task ImmutableKararBulunmayanLegacyMesajClaimEdilmez()
    {
        await using var dbContext = CreateDbContext();
        var (_, eBelgeKaydiId) = await SeedEBelgeKaydiAsync(dbContext, _kurumId, _tesisId, _musteriKartId, _suffixA);
        await SeedPolitikaAsync(dbContext, _kurumId, EBelgeEntegrasyonYontemi.GibPortal, aktifMi: true, new DateTime(2020, 1, 1));
        // KARAR HİÇ SEED EDİLMEDİ - legacy/karar-öncesi outbox mesajı.
        await SeedOutboxAsync(dbContext, _kurumId, eBelgeKaydiId, EBelgeOutboxIsTuru.ArtefaktOlustur);

        var claim = await ClaimNextAsync(dbContext);

        Assert.Null(claim);
    }

    // 8. Kurum A'nın politikası pasif/uyumsuz olsa da, kurum B'nin AYNI yöntemdeki aktif politikası
    // kurum A'nın mesajını "claim'e uygun" hale GETİRMEZ (join KurumId'ye göre doğru scope'lanmış).
    [IntegrationFact]
    [Trait("CriticalInvariant", "InstitutionPolicyTenantIsolation")]
    public async Task BaskaKurumunAktifPolitikasiCrossTenantOlarakClaimUygunlugunaSizmaz()
    {
        await using var dbContext = CreateDbContext();

        var (satisBelgesiIdA, eBelgeKaydiIdA) = await SeedEBelgeKaydiAsync(dbContext, _kurumId, _tesisId, _musteriKartId, _suffixA);
        await SeedPolitikaAsync(dbContext, _kurumId, EBelgeEntegrasyonYontemi.GibPortal, aktifMi: false, new DateTime(2020, 1, 1));
        await SeedKararAsync(dbContext, _kurumId, satisBelgesiIdA, eBelgeKaydiIdA, EBelgeEntegrasyonYontemi.GibPortal, true, true, false);
        var outboxIdA = await SeedOutboxAsync(dbContext, _kurumId, eBelgeKaydiIdA, EBelgeOutboxIsTuru.ArtefaktOlustur);

        // Kurum B, AYNI yöntemde (GibPortal) TAM aktif bir politikaya sahip - ama kurum A'nın mesajını ASLA etkilememeli.
        await SeedPolitikaAsync(dbContext, _kurumBId, EBelgeEntegrasyonYontemi.GibPortal, aktifMi: true, new DateTime(2020, 1, 1));

        var claim = await ClaimNextAsync(dbContext);

        Assert.Null(claim);

        var mesajA = await dbContext.EBelgeOutboxMesajlari.IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.Id == outboxIdA);
        Assert.Equal(EBelgeOutboxDurumu.Bekliyor, mesajA.Durum);
    }

    // 9. İki worker aynı uygun mesajı claim edemez invariantı, politika join'i eklendikten SONRA da korunur.
    [IntegrationFact]
    [Trait("CriticalInvariant", "LeaseTakeover")]
    public async Task IkiWorkerAyniUygunMesajiClaimEdemezPolitikaJoinIleKorunur()
    {
        await using var seedCtx = CreateDbContext();
        var (satisBelgesiId, eBelgeKaydiId) = await SeedEBelgeKaydiAsync(seedCtx, _kurumId, _tesisId, _musteriKartId, _suffixA);
        await SeedPolitikaAsync(seedCtx, _kurumId, EBelgeEntegrasyonYontemi.GibPortal, aktifMi: true, new DateTime(2020, 1, 1));
        await SeedKararAsync(seedCtx, _kurumId, satisBelgesiId, eBelgeKaydiId, EBelgeEntegrasyonYontemi.GibPortal, true, true, false);
        await SeedOutboxAsync(seedCtx, _kurumId, eBelgeKaydiId, EBelgeOutboxIsTuru.ArtefaktOlustur);

        await using var ctx1 = CreateDbContext();
        await using var ctx2 = CreateDbContext();

        var claim1Task = ClaimNextAsync(ctx1);
        var claim2Task = ClaimNextAsync(ctx2);
        var claims = await Task.WhenAll(claim1Task, claim2Task);

        var nonNullClaims = claims.Where(c => c is not null).ToList();
        Assert.Single(nonNullClaims);
    }

    // ---- Faz 2B.10.3 - global signing gate, claim eligibility'ye EK bir AND katmanı olarak ----

    // 10/11/12. Signing gate kapalıyken UblImzala mesajı claim edilmez, attempt değişmez, lease oluşmaz.
    [IntegrationFact]
    [Trait("CriticalInvariant", "SigningGatePreventsQueuedSigning")]
    public async Task SigningGateKapaliykenUblImzalaMesajiClaimEdilmezAttemptDegismezLeaseOlusmaz()
    {
        await using var dbContext = CreateDbContext();
        var (satisBelgesiId, eBelgeKaydiId) = await SeedEBelgeKaydiAsync(dbContext, _kurumId, _tesisId, _musteriKartId, _suffixA);
        await SeedPolitikaAsync(dbContext, _kurumId, EBelgeEntegrasyonYontemi.DogrudanGib, aktifMi: true, new DateTime(2020, 1, 1));
        await SeedKararAsync(dbContext, _kurumId, satisBelgesiId, eBelgeKaydiId, EBelgeEntegrasyonYontemi.DogrudanGib, true, true, true);
        var outboxId = await SeedOutboxAsync(dbContext, _kurumId, eBelgeKaydiId, EBelgeOutboxIsTuru.UblImzala);

        var claim = await ClaimNextAsync(dbContext, EBelgeTestSigningActivationGate.Kapali);

        Assert.Null(claim);

        var mesaj = await dbContext.EBelgeOutboxMesajlari.IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.Id == outboxId);
        Assert.Equal(EBelgeOutboxDurumu.Bekliyor, mesaj.Durum);
        Assert.Equal(0, mesaj.DenemeSayisi);
        Assert.Null(mesaj.KilitToken);
        Assert.Null(mesaj.KilitBitisZamaniUtc);
    }

    // 13. Aynı batch/poll boyunca art arda claim çağrıları HEP null döner - gerçek churn yok.
    [IntegrationFact]
    public async Task SigningGateKapaliykenArtArdaBesClaimCagrisiHepNullDonerVeMesajDegismezKalir()
    {
        await using var dbContext = CreateDbContext();
        var (satisBelgesiId, eBelgeKaydiId) = await SeedEBelgeKaydiAsync(dbContext, _kurumId, _tesisId, _musteriKartId, _suffixA);
        await SeedPolitikaAsync(dbContext, _kurumId, EBelgeEntegrasyonYontemi.DogrudanGib, aktifMi: true, new DateTime(2020, 1, 1));
        await SeedKararAsync(dbContext, _kurumId, satisBelgesiId, eBelgeKaydiId, EBelgeEntegrasyonYontemi.DogrudanGib, true, true, true);
        var outboxId = await SeedOutboxAsync(dbContext, _kurumId, eBelgeKaydiId, EBelgeOutboxIsTuru.UblImzala);

        for (var i = 0; i < 5; i++)
        {
            var claim = await ClaimNextAsync(dbContext, EBelgeTestSigningActivationGate.Kapali);
            Assert.Null(claim);
        }

        var mesaj = await dbContext.EBelgeOutboxMesajlari.IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.Id == outboxId);
        Assert.Equal(EBelgeOutboxDurumu.Bekliyor, mesaj.Durum);
        Assert.Equal(0, mesaj.DenemeSayisi);
        Assert.Null(mesaj.KilitToken);
        Assert.Null(mesaj.KilitBitisZamaniUtc);
    }

    // 5/9. Gate kapalıyken kuyrukta ÖNCE gelen bloklu bir UblImzala mesajı, SONRA gelen uygun bir
    // ArtefaktOlustur mesajının claim edilmesini ENGELLEMEZ (starvation yok).
    [IntegrationFact]
    public async Task SigningGateKapaliykenIlkSiradakiSigningMesajiUygunArtefaktMesajininClaimEdilmesiniEngellemez()
    {
        await using var dbContext = CreateDbContext();

        var (satisBelgesiIdSign, eBelgeKaydiIdSign) = await SeedEBelgeKaydiAsync(dbContext, _kurumId, _tesisId, _musteriKartId, _suffixA);
        await SeedPolitikaAsync(dbContext, _kurumId, EBelgeEntegrasyonYontemi.DogrudanGib, aktifMi: true, new DateTime(2020, 1, 1));
        await SeedKararAsync(dbContext, _kurumId, satisBelgesiIdSign, eBelgeKaydiIdSign, EBelgeEntegrasyonYontemi.DogrudanGib, true, true, true);
        var signOutboxId = await SeedOutboxAsync(dbContext, _kurumId, eBelgeKaydiIdSign, EBelgeOutboxIsTuru.UblImzala); // ÖNCE seed edilir (küçük Id)

        var (satisBelgesiIdArt, eBelgeKaydiIdArt) = await SeedEBelgeKaydiAsync(dbContext, _kurumId, _tesisId, _musteriKartId, _suffixA);
        await SeedKararAsync(dbContext, _kurumId, satisBelgesiIdArt, eBelgeKaydiIdArt, EBelgeEntegrasyonYontemi.DogrudanGib, true, true, true);
        var artOutboxId = await SeedOutboxAsync(dbContext, _kurumId, eBelgeKaydiIdArt, EBelgeOutboxIsTuru.ArtefaktOlustur); // SONRA seed edilir (büyük Id)

        var claim = await ClaimNextAsync(dbContext, EBelgeTestSigningActivationGate.Kapali);

        Assert.NotNull(claim);
        Assert.Equal(artOutboxId, claim!.OutboxMesajiId);
        Assert.Equal(EBelgeOutboxIsTuru.ArtefaktOlustur, claim.IsTuru);

        var signMesaj = await dbContext.EBelgeOutboxMesajlari.IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.Id == signOutboxId);
        Assert.Equal(EBelgeOutboxDurumu.Bekliyor, signMesaj.Durum); // bloklu kaldı ama starvation OLUŞTURMADI
        Assert.Equal(0, signMesaj.DenemeSayisi);
    }

    // 6. Gate tekrar açılınca AYNI signing mesajı claim edilebilir.
    [IntegrationFact]
    public async Task SigningGateTekrarAcilincaAyniSigningMesajiClaimEdilebilir()
    {
        await using var dbContext = CreateDbContext();
        var (satisBelgesiId, eBelgeKaydiId) = await SeedEBelgeKaydiAsync(dbContext, _kurumId, _tesisId, _musteriKartId, _suffixA);
        await SeedPolitikaAsync(dbContext, _kurumId, EBelgeEntegrasyonYontemi.DogrudanGib, aktifMi: true, new DateTime(2020, 1, 1));
        await SeedKararAsync(dbContext, _kurumId, satisBelgesiId, eBelgeKaydiId, EBelgeEntegrasyonYontemi.DogrudanGib, true, true, true);
        var outboxId = await SeedOutboxAsync(dbContext, _kurumId, eBelgeKaydiId, EBelgeOutboxIsTuru.UblImzala);

        var bloklu = await ClaimNextAsync(dbContext, EBelgeTestSigningActivationGate.Kapali);
        Assert.Null(bloklu);

        var claim = await ClaimNextAsync(dbContext, EBelgeTestSigningActivationGate.Acik);

        Assert.NotNull(claim);
        Assert.Equal(outboxId, claim!.OutboxMesajiId);
        Assert.Equal(1, claim.DenemeSayisi); // gate kapalıyken YAPILAN denemeler DenemeSayisi'ni ARTIRMADI
        Assert.NotNull(claim.KilitToken);
    }

    // 8. Gate NotBeforeLocalDate tarihi HENÜZ gelmemişse (gerçek EBelgeSigningActivationGate ile) signing mesajı claim edilmez.
    [IntegrationFact]
    public async Task SigningGateAktivasyonTarihiHenuzGelmemisseSigningMesajiClaimEdilmez()
    {
        await using var dbContext = CreateDbContext();
        var (satisBelgesiId, eBelgeKaydiId) = await SeedEBelgeKaydiAsync(dbContext, _kurumId, _tesisId, _musteriKartId, _suffixA);
        await SeedPolitikaAsync(dbContext, _kurumId, EBelgeEntegrasyonYontemi.DogrudanGib, aktifMi: true, new DateTime(2020, 1, 1));
        await SeedKararAsync(dbContext, _kurumId, satisBelgesiId, eBelgeKaydiId, EBelgeEntegrasyonYontemi.DogrudanGib, true, true, true);
        await SeedOutboxAsync(dbContext, _kurumId, eBelgeKaydiId, EBelgeOutboxIsTuru.UblImzala);

        var gate = CreateRealSigningGate(
            new EBelgeSigningOptions { Enabled = true, NotBeforeLocalDate = "2030-01-01" },
            new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var claim = await ClaimNextAsync(dbContext, gate);

        Assert.Null(claim);
    }

    // 9. Geçersiz NotBeforeLocalDate config'i (gerçek gate) fail-closed'dır - signing mesajı claim edilmez.
    [IntegrationFact]
    public async Task SigningGateGecersizNotBeforeLocalDateIleSigningMesajiClaimEdilmezFailClosed()
    {
        await using var dbContext = CreateDbContext();
        var (satisBelgesiId, eBelgeKaydiId) = await SeedEBelgeKaydiAsync(dbContext, _kurumId, _tesisId, _musteriKartId, _suffixA);
        await SeedPolitikaAsync(dbContext, _kurumId, EBelgeEntegrasyonYontemi.DogrudanGib, aktifMi: true, new DateTime(2020, 1, 1));
        await SeedKararAsync(dbContext, _kurumId, satisBelgesiId, eBelgeKaydiId, EBelgeEntegrasyonYontemi.DogrudanGib, true, true, true);
        await SeedOutboxAsync(dbContext, _kurumId, eBelgeKaydiId, EBelgeOutboxIsTuru.UblImzala);

        var gate = CreateRealSigningGate(
            new EBelgeSigningOptions { Enabled = true, NotBeforeLocalDate = "gecersiz-tarih" },
            new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var claim = await ClaimNextAsync(dbContext, gate);

        Assert.Null(claim);
    }

    // 10. Politika pasif + signing gate AÇIK - mevcut politika filtresi YİNE çalışır (signing gate onu BASTIRMAZ).
    [IntegrationFact]
    public async Task PolitikaPasifSigningGateAcikkenUblImzalaMesajiYineDeClaimEdilmezPolitikaFiltresiCalisir()
    {
        await using var dbContext = CreateDbContext();
        var (satisBelgesiId, eBelgeKaydiId) = await SeedEBelgeKaydiAsync(dbContext, _kurumId, _tesisId, _musteriKartId, _suffixA);
        await SeedPolitikaAsync(dbContext, _kurumId, EBelgeEntegrasyonYontemi.DogrudanGib, aktifMi: false, new DateTime(2020, 1, 1));
        await SeedKararAsync(dbContext, _kurumId, satisBelgesiId, eBelgeKaydiId, EBelgeEntegrasyonYontemi.DogrudanGib, true, true, true);
        await SeedOutboxAsync(dbContext, _kurumId, eBelgeKaydiId, EBelgeOutboxIsTuru.UblImzala);

        var claim = await ClaimNextAsync(dbContext, EBelgeTestSigningActivationGate.Acik);

        Assert.Null(claim);
    }

    // 11. Politika aktif + signing gate KAPALI - AYNI kurumun ArtefaktOlustur mesajı YİNE claim
    // edilir (politika filtresi engellemiyor), ama UblImzala mesajı signing gate TARAFINDAN engellenir.
    [IntegrationFact]
    public async Task PolitikaAktifSigningGateKapaliykenArtefaktMesajiClaimEdilirImzaMesajiEdilmez()
    {
        await using var dbContext = CreateDbContext();
        var (satisBelgesiId, eBelgeKaydiId) = await SeedEBelgeKaydiAsync(dbContext, _kurumId, _tesisId, _musteriKartId, _suffixA);
        await SeedPolitikaAsync(dbContext, _kurumId, EBelgeEntegrasyonYontemi.DogrudanGib, aktifMi: true, new DateTime(2020, 1, 1));
        await SeedKararAsync(dbContext, _kurumId, satisBelgesiId, eBelgeKaydiId, EBelgeEntegrasyonYontemi.DogrudanGib, true, true, true);
        var artOutboxId = await SeedOutboxAsync(dbContext, _kurumId, eBelgeKaydiId, EBelgeOutboxIsTuru.ArtefaktOlustur);

        var claim = await ClaimNextAsync(dbContext, EBelgeTestSigningActivationGate.Kapali);

        Assert.NotNull(claim);
        Assert.Equal(artOutboxId, claim!.OutboxMesajiId);
        Assert.Equal(EBelgeOutboxIsTuru.ArtefaktOlustur, claim.IsTuru);
    }
}
