using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.SatisBelgeleri;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Muhasebe.SatisBelgeleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using STYS.Muhasebe.SatisBelgeleri.Services;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// Faz 2B.7.1 görev md.7 - `EBelgeSigningBackfillService`'in, aktivasyon tarihinden ÖNCE
/// oluşturulmuş (bu yüzden canlı akışın "yalnız İLK GERÇEK oluşturmada mesaj ekle" kuralı
/// gereği kendiliğinden kuyruğa GİRMEMİŞ) `UnsignedUblHazir` kayıtlar için, aktivasyon SONRASI,
/// KONTROLLÜ ve İDEMPOTENT bir şekilde `UblImzala` outbox mesajı oluşturduğunu doğrular. GERÇEK
/// SQL Server ile - sidecar/gerçek imzalama GEREKMEZ (bu servis yalnız outbox mesajı ekler,
/// imzalama YAPMAZ).
/// </summary>
[Trait("Category", "Integration")]
[Collection(SqlServerIntegrationCollection.Name)]
public class EBelgeSigningBackfillServiceIntegrationTests : IAsyncLifetime
{
    private const string TestMarker = "EBI-2B71-BF";

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
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM [muhasebe].[EBelgeArtifactlari] WHERE [KurumId] = {_kurumId}");
        await SatisBelgesiMuhasebeTestSupport.CleanupAsync(dbContext, _uniqueSuffix, _tesisId, _kurumId, _ilId);
    }

    private static StysAppDbContext CreateDbContext() => SatisBelgesiMuhasebeTestSupport.CreateDbContext();

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
                    KdvUygulamaTipi = (int)STYS.Muhasebe.Kdv.Enums.KdvUygulamaTipi.Kdvli,
                    KdvOrani = 20m
                }
            ]
        };

        var created = await service.CreateAsync(request);
        return created.Id!.Value;
    }

    private async Task<int> SeedEBelgeKaydiAsync(StysAppDbContext dbContext, EBelgeKaydiDurumu durum)
    {
        var satisBelgesiId = await CreateSatisBelgesiIdAsync(dbContext);
        var eBelgeKaydi = new EBelgeKaydi
        {
            KurumId = _kurumId,
            SatisBelgesiId = satisBelgesiId,
            EBelgeUuid = Guid.NewGuid().ToString("D"),
            EBelgeKanali = EBelgeKanali.EArsiv,
            Durum = durum,
        };
        dbContext.EBelgeKayitlari.Add(eBelgeKaydi);
        await dbContext.SaveChangesAsync();
        return eBelgeKaydi.Id;
    }

    private static EBelgeSigningBackfillService CreateService(StysAppDbContext dbContext, bool gateAcik) =>
        new(dbContext, new FixedActivationGate(gateAcik), NullLogger<EBelgeSigningBackfillService>.Instance);

    private sealed class FixedActivationGate : IEBelgeSigningActivationGate
    {
        private readonly bool _sonuc;
        public FixedActivationGate(bool sonuc) => _sonuc = sonuc;
        public bool ShouldCreateSigningMessage() => _sonuc;
    }

    [IntegrationFact]
    public async Task GateKapaliykenHicbirMesajOlusturulmazSifirDoner()
    {
        await using var dbContext = CreateDbContext();
        var eBelgeKaydiId = await SeedEBelgeKaydiAsync(dbContext, EBelgeKaydiDurumu.UnsignedUblHazir);

        var service = CreateService(dbContext, gateAcik: false);
        var sonuc = await service.BackfillAsync(_kurumId, CancellationToken.None);

        Assert.Equal(0, sonuc);

        await using var verifyCtx = CreateDbContext();
        Assert.False(await verifyCtx.EBelgeOutboxMesajlari.AnyAsync(o => o.EBelgeKaydiId == eBelgeKaydiId));
    }

    [IntegrationFact]
    public async Task GateAcikkenUygunKayitIcinTekMesajOlusturulur()
    {
        await using var dbContext = CreateDbContext();
        var eBelgeKaydiId = await SeedEBelgeKaydiAsync(dbContext, EBelgeKaydiDurumu.UnsignedUblHazir);

        var service = CreateService(dbContext, gateAcik: true);
        var sonuc = await service.BackfillAsync(_kurumId, CancellationToken.None);

        Assert.Equal(1, sonuc);

        await using var verifyCtx = CreateDbContext();
        var mesaj = await verifyCtx.EBelgeOutboxMesajlari.AsNoTracking().SingleAsync(o => o.EBelgeKaydiId == eBelgeKaydiId);
        Assert.Equal(EBelgeOutboxIsTuru.UblImzala, mesaj.IsTuru);
        Assert.Equal(EBelgeOutboxDurumu.Bekliyor, mesaj.Durum);
    }

    [IntegrationFact]
    public async Task ZatenUblImzalaMesajiOlanKayitAtlanir()
    {
        await using var dbContext = CreateDbContext();
        var eBelgeKaydiId = await SeedEBelgeKaydiAsync(dbContext, EBelgeKaydiDurumu.UnsignedUblHazir);
        dbContext.EBelgeOutboxMesajlari.Add(new EBelgeOutboxMesaji
        {
            KurumId = _kurumId,
            EBelgeKaydiId = eBelgeKaydiId,
            IsTuru = EBelgeOutboxIsTuru.UblImzala,
            Durum = EBelgeOutboxDurumu.Bekliyor,
            DenemeSayisi = 0,
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext, gateAcik: true);
        var sonuc = await service.BackfillAsync(_kurumId, CancellationToken.None);

        Assert.Equal(0, sonuc);

        await using var verifyCtx = CreateDbContext();
        var sayi = await verifyCtx.EBelgeOutboxMesajlari.CountAsync(o => o.EBelgeKaydiId == eBelgeKaydiId && o.IsTuru == EBelgeOutboxIsTuru.UblImzala);
        Assert.Equal(1, sayi); // ikinci mesaj EKLENMEDİ
    }

    [IntegrationFact]
    public async Task ZatenSignedReadyArtefaktiOlanKayitAtlanir()
    {
        await using var dbContext = CreateDbContext();
        var eBelgeKaydiId = await SeedEBelgeKaydiAsync(dbContext, EBelgeKaydiDurumu.UnsignedUblHazir);

        // CK_EBelgeArtifactlari_ImzaAlanlari kısıtı, SignedReady için Kaynak* alanlarının TÜMÜNÜN
        // dolu olmasını (GERÇEK bir kaynak Unsigned artefaktına işaret etmesini) ZORUNLU kılar -
        // bu yüzden önce GERÇEK bir Unsigned artefakt seed edilir, SignedReady ONA bağlanır.
        var unsignedArtifact = new EBelgeArtifact
        {
            KurumId = _kurumId,
            EBelgeKaydiId = eBelgeKaydiId,
            ArtifactTipi = EBelgeArtifactTipi.UblXml,
            ArtifactAsamasi = EBelgeArtifactAsamasi.Unsigned,
            RuleSetId = "test-kural-seti",
            SnapshotSchemaVersion = 2,
            KaynakSnapshotSha256 = new string('a', 64),
            ArtifactSha256 = new string('c', 64),
            Icerik = "<unsigned/>"u8.ToArray(),
            MimeType = "application/xml",
            DosyaAdi = "unsigned.xml",
            OlusturulmaZamaniUtc = DateTime.UtcNow,
        };
        dbContext.EBelgeArtifactlari.Add(unsignedArtifact);
        await dbContext.SaveChangesAsync();

        dbContext.EBelgeArtifactlari.Add(new EBelgeArtifact
        {
            KurumId = _kurumId,
            EBelgeKaydiId = eBelgeKaydiId,
            ArtifactTipi = EBelgeArtifactTipi.UblXml,
            ArtifactAsamasi = EBelgeArtifactAsamasi.SignedReady,
            RuleSetId = "test-kural-seti",
            SnapshotSchemaVersion = 2,
            KaynakSnapshotSha256 = new string('a', 64),
            ArtifactSha256 = new string('b', 64),
            Icerik = "<imzali/>"u8.ToArray(),
            MimeType = "application/xml",
            DosyaAdi = "imzali.xml",
            OlusturulmaZamaniUtc = DateTime.UtcNow,
            KaynakArtifactId = unsignedArtifact.Id,
            KaynakArtifactSha256 = unsignedArtifact.ArtifactSha256,
            ImzaProfili = "dummy",
            ImzaAlgoritmasi = "dummy",
            DigestAlgoritmasi = "dummy",
            ImzalayanSertifikaSha256ParmakIzi = "dummy",
            ImzalamaZamaniUtc = DateTime.UtcNow,
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext, gateAcik: true);
        var sonuc = await service.BackfillAsync(_kurumId, CancellationToken.None);

        Assert.Equal(0, sonuc);

        await using var verifyCtx = CreateDbContext();
        Assert.False(await verifyCtx.EBelgeOutboxMesajlari.AnyAsync(o => o.EBelgeKaydiId == eBelgeKaydiId));
    }

    [IntegrationFact]
    public async Task FarkliDurumdakiKayitAtlanir()
    {
        await using var dbContext = CreateDbContext();
        var eBelgeKaydiId = await SeedEBelgeKaydiAsync(dbContext, EBelgeKaydiDurumu.SnapshotHazir);

        var service = CreateService(dbContext, gateAcik: true);
        var sonuc = await service.BackfillAsync(_kurumId, CancellationToken.None);

        Assert.Equal(0, sonuc);

        await using var verifyCtx = CreateDbContext();
        Assert.False(await verifyCtx.EBelgeOutboxMesajlari.AnyAsync(o => o.EBelgeKaydiId == eBelgeKaydiId));
    }

    [IntegrationFact]
    public async Task BaskaKurumaAitUygunKayitDahilEdilmez()
    {
        await using var dbContext = CreateDbContext();
        var eBelgeKaydiId = await SeedEBelgeKaydiAsync(dbContext, EBelgeKaydiDurumu.UnsignedUblHazir);

        var service = CreateService(dbContext, gateAcik: true);
        var sonuc = await service.BackfillAsync(_kurumId + 999_000, CancellationToken.None);

        Assert.Equal(0, sonuc);

        await using var verifyCtx = CreateDbContext();
        Assert.False(await verifyCtx.EBelgeOutboxMesajlari.AnyAsync(o => o.EBelgeKaydiId == eBelgeKaydiId));
    }

    [IntegrationFact]
    public async Task AyniCagriIkinciKezYapilirsaIdempotentOlurTekrarMesajOlusturulmaz()
    {
        await using var dbContext = CreateDbContext();
        var eBelgeKaydiId = await SeedEBelgeKaydiAsync(dbContext, EBelgeKaydiDurumu.UnsignedUblHazir);

        var service = CreateService(dbContext, gateAcik: true);
        var ilkSonuc = await service.BackfillAsync(_kurumId, CancellationToken.None);
        Assert.Equal(1, ilkSonuc);

        await using var dbContext2 = CreateDbContext();
        var service2 = CreateService(dbContext2, gateAcik: true);
        var ikinciSonuc = await service2.BackfillAsync(_kurumId, CancellationToken.None);
        Assert.Equal(0, ikinciSonuc);

        await using var verifyCtx = CreateDbContext();
        var sayi = await verifyCtx.EBelgeOutboxMesajlari.CountAsync(o => o.EBelgeKaydiId == eBelgeKaydiId && o.IsTuru == EBelgeOutboxIsTuru.UblImzala);
        Assert.Equal(1, sayi);
    }
}
