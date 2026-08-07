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

    private static Task<EBelgeOutboxClaimLeaseResultDto?> ClaimNextAsync(StysAppDbContext dbContext) =>
        new EBelgeOutboxClaimLeaseService(dbContext).TryClaimNextAsync(TimeSpan.FromSeconds(60));

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
}
