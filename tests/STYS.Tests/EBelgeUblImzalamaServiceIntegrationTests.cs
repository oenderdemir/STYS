using System.Collections.Immutable;
using System.Security.Cryptography;
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
/// Faz 2B.7 - GERÇEK SQL Server + GERÇEK renderer + GERÇEK Java Saxon sidecar + GERÇEK (test)
/// RSA sertifikasıyla `EBelgeUblImzalamaService`'in atomik-transaction/lease-safe outbox tüketim
/// akışını, imzalı XML'in SIFIR-tolerans XSD + Schematron doğrulamasından geçtiğini, kaynak/imzalı
/// hash zincirinin kalıcılaştığını ve idempotency/çakışma davranışını doğrular (bkz. görev md.12,
/// md.17, md.20, md.25 "gerçek sidecar gereken en az bir imzalama outbox testi"). Mock/sahte
/// imzalayıcı/doğrulayıcı/validator KULLANILMAZ - yalnız üretim sınıfları, test-only bir sertifika
/// sağlayıcısıyla (bkz. EBelgeTestSertifikaSaglayici) birlikte.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SqlServerIntegrationCollection.Name)]
public class EBelgeUblImzalamaServiceIntegrationTests : IAsyncLifetime, IClassFixture<SchematronSidecarProcessFixture>
{
    private const string TestMarker = "EBI-2B7";

    private readonly SchematronSidecarProcessFixture _sidecarFixture;
    private string _uniqueSuffix = TestMarker;
    private int _kurumId;
    private int _ilId;
    private int _tesisId;
    private int _musteriKartId;

    public EBelgeUblImzalamaServiceIntegrationTests(SchematronSidecarProcessFixture sidecarFixture)
    {
        _sidecarFixture = sidecarFixture;
    }

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

        // Kendine-referanslı (KaynakArtifactId) Restrict FK - genel temizlik yardımcısı bunu
        // BİLMEZ, bu yüzden artefaktlar ÖNCE burada, TEK bir DELETE ifadesiyle (SignedReady +
        // Unsigned birlikte) silinir (bkz. EBelgeArtefaktOlusturmaServiceIntegrationTests ile AYNI desen).
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

    /// <summary>GERÇEK renderer + GERÇEK sidecar ile üretilmiş, GEÇERLİ bir Unsigned UBL artefaktını, ona bağlı YENİ bir (Durum=UnsignedUblHazir) EBelgeKaydi ile birlikte kalıcılaştırır.</summary>
    private async Task<(int eBelgeKaydiId, EBelgeArtifact unsignedArtifact)> SeedUnsignedArtifactAsync(StysAppDbContext dbContext)
    {
        if (_sidecarFixture.BaseUrl is null)
        {
            Assert.Fail($"Sidecar ayağa kaldırılamadı: {_sidecarFixture.AtlamaNedeni}");
        }

        var satisBelgesiId = await CreateSatisBelgesiIdAsync(dbContext);
        var eBelgeKaydi = new EBelgeKaydi
        {
            KurumId = _kurumId,
            SatisBelgesiId = satisBelgesiId,
            EBelgeUuid = Guid.NewGuid().ToString("D"),
            EBelgeKanali = EBelgeKanali.EArsiv,
            Durum = EBelgeKaydiDurumu.UnsignedUblHazir,
        };
        dbContext.EBelgeKayitlari.Add(eBelgeKaydi);
        await dbContext.SaveChangesAsync();

        var renderer = RealRendererTestSupport.CreateRealRenderer(_sidecarFixture.BaseUrl!);
        var renderSonucu = await renderer.RenderAsync(EBelgeUblRendererTestVerisi.GecerliSnapshot(), CancellationToken.None);

        var unsigned = new EBelgeArtifact
        {
            KurumId = _kurumId,
            EBelgeKaydiId = eBelgeKaydi.Id,
            ArtifactTipi = EBelgeArtifactTipi.UblXml,
            ArtifactAsamasi = EBelgeArtifactAsamasi.Unsigned,
            RuleSetId = renderSonucu.KuralSetiKimligi,
            SnapshotSchemaVersion = int.Parse(EBelgeCanonicalSnapshotV2Reader.SupportedSnapshotSchemaVersion),
            KaynakSnapshotSha256 = new string('a', 64),
            ArtifactSha256 = renderSonucu.UnsignedUblSha256,
            Icerik = renderSonucu.UnsignedUblUtf8.ToArray(),
            MimeType = "application/xml",
            DosyaAdi = "unsigned-test.xml",
            OlusturulmaZamaniUtc = DateTime.UtcNow,
        };
        dbContext.EBelgeArtifactlari.Add(unsigned);
        await dbContext.SaveChangesAsync();

        return (eBelgeKaydi.Id, unsigned);
    }

    private async Task<EBelgeOutboxClaimLeaseResultDto> SeedAndClaimUblImzalaOutboxAsync(StysAppDbContext dbContext, int eBelgeKaydiId, TimeSpan? leaseDuration = null)
    {
        dbContext.EBelgeOutboxMesajlari.Add(new EBelgeOutboxMesaji
        {
            KurumId = _kurumId,
            EBelgeKaydiId = eBelgeKaydiId,
            IsTuru = EBelgeOutboxIsTuru.UblImzala,
            Durum = EBelgeOutboxDurumu.Bekliyor,
            DenemeSayisi = 0,
        });
        await dbContext.SaveChangesAsync();

        var claimService = new EBelgeOutboxClaimLeaseService(dbContext);
        var claim = await claimService.TryClaimNextAsync(leaseDuration ?? TimeSpan.FromMinutes(5));
        Assert.NotNull(claim);
        return claim!;
    }

    private static EBelgeUblImzalamaTalebi TalepFromClaim(EBelgeOutboxClaimLeaseResultDto claim, int kurumId, int eBelgeKaydiId)
        => new(kurumId, eBelgeKaydiId, claim.OutboxMesajiId, claim.KilitToken, claim.KilitBitisZamaniUtc);

    private static Task BackdateLeaseExpiryAsync(StysAppDbContext dbContext, int outboxMesajiId) =>
        dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [muhasebe].[EBelgeOutboxMesajlari] SET [KilitBitisZamaniUtc] = DATEADD(MINUTE, -1, SYSUTCDATETIME()) WHERE [Id] = {outboxMesajiId}");

    private EBelgeUblImzalamaService CreateService(StysAppDbContext dbContext, TimeProvider? timeProvider = null)
    {
        if (_sidecarFixture.BaseUrl is null)
        {
            Assert.Fail($"Sidecar ayağa kaldırılamadı: {_sidecarFixture.AtlamaNedeni}");
        }

        var kuralSeti = EBelgeUblRendererTestVerisi.KuralSetiYukle();
        var xsdValidator = new EBelgeUblXsdValidator(kuralSeti);
        var http = new HttpClient { BaseAddress = new Uri(_sidecarFixture.BaseUrl!), Timeout = TimeSpan.FromSeconds(15) };
        var schematronValidator = new SaxonSidecarEBelgeSchematronValidator(http);

        return new EBelgeUblImzalamaService(
            dbContext,
            new EBelgeXmlImzalayici(new EBelgeTestSertifikaSaglayici(), new EBelgeTestSertifikaGuvenPolicy()),
            new EBelgeXmlImzaDogrulayici(),
            xsdValidator,
            schematronValidator,
            new EBelgeOutboxLeaseTransitionService(dbContext),
            timeProvider ?? TimeProvider.System,
            NullLogger<EBelgeUblImzalamaService>.Instance);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _zaman;
        public FixedTimeProvider(DateTimeOffset zaman) => _zaman = zaman;
        public override DateTimeOffset GetUtcNow() => _zaman;
    }

    // ---- Başarılı akış: gerçek imza + sıfır-tolerans XSD/Schematron + hash zinciri ----

    [IntegrationFact]
    public async Task GecerliImzaTamAtomikBasariylaSignedReadyArtefaktUretirVeHashZinciriDogrulanir()
    {
        await using var dbContext = CreateDbContext();
        var (eBelgeKaydiId, unsignedArtifact) = await SeedUnsignedArtifactAsync(dbContext);
        var claim = await SeedAndClaimUblImzalaOutboxAsync(dbContext, eBelgeKaydiId);

        var sabitZaman = new DateTimeOffset(2026, 8, 5, 10, 0, 0, TimeSpan.Zero);
        var service = CreateService(dbContext, new FixedTimeProvider(sabitZaman));
        var sonuc = await service.ImzalaAsync(TalepFromClaim(claim, _kurumId, eBelgeKaydiId));

        Assert.NotNull(sonuc);
        Assert.True(sonuc!.BasariliMi, $"{sonuc.SonucTuru}: {sonuc.HataKodu} {sonuc.HataMesaji}");

        await using var verifyCtx = CreateDbContext();
        var signed = await verifyCtx.EBelgeArtifactlari.AsNoTracking()
            .SingleAsync(a => a.EBelgeKaydiId == eBelgeKaydiId && a.ArtifactAsamasi == EBelgeArtifactAsamasi.SignedReady);
        var kayit = await verifyCtx.EBelgeKayitlari.AsNoTracking().SingleAsync(x => x.Id == eBelgeKaydiId);
        var outbox = await verifyCtx.EBelgeOutboxMesajlari.AsNoTracking().SingleAsync(x => x.Id == claim.OutboxMesajiId);

        // Kaynak/imzalı hash zinciri (md.15) - imzalı içerik EXACT byte'lar üzerinden saklanır.
        Assert.Equal(unsignedArtifact.Id, signed.KaynakArtifactId);
        Assert.Equal(unsignedArtifact.ArtifactSha256, signed.KaynakArtifactSha256);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(signed.Icerik)), signed.ArtifactSha256, ignoreCase: true);
        Assert.NotNull(signed.ImzaProfili);
        Assert.NotNull(signed.ImzaAlgoritmasi);
        Assert.NotNull(signed.DigestAlgoritmasi);
        Assert.NotNull(signed.ImzalayanSertifikaSha256ParmakIzi);
        Assert.Equal(sabitZaman.UtcDateTime, signed.ImzalamaZamaniUtc);
        Assert.EndsWith("-imzali.xml", signed.DosyaAdi, StringComparison.Ordinal);

        // Bağımsız doğrulayıcı, kalıcılaşan İMZALI içerik üzerinde de GEÇER (md.11) - servisin
        // kendi imza+doğrulama+XSD(sıfır tolerans)+Schematron(sıfır ihlal) zincirinin SONUCU
        // olarak zaten dolaylı kanıtlanmıştır, burada AYRICA doğrudan tekrar kontrol edilir.
        var dogrulama = await new EBelgeXmlImzaDogrulayici().DogrulaAsync(ImmutableArray.Create(signed.Icerik), CancellationToken.None);
        Assert.True(dogrulama.GecerliMi, $"{dogrulama.HataKodu}: {dogrulama.HataMesaji}");

        Assert.Equal(EBelgeKaydiDurumu.SignedReady, kayit.Durum);
        Assert.Equal(EBelgeOutboxDurumu.Tamamlandi, outbox.Durum);
        Assert.Null(outbox.KilitToken);
        Assert.Null(outbox.KilitBitisZamaniUtc);
    }

    [IntegrationFact]
    public async Task TamOutboxAkisiImzalamaHandlerIleBirlikteCalisir()
    {
        await using var seedCtx = CreateDbContext();
        var (eBelgeKaydiId, _) = await SeedUnsignedArtifactAsync(seedCtx);

        seedCtx.EBelgeOutboxMesajlari.Add(new EBelgeOutboxMesaji
        {
            KurumId = _kurumId,
            EBelgeKaydiId = eBelgeKaydiId,
            IsTuru = EBelgeOutboxIsTuru.UblImzala,
            Durum = EBelgeOutboxDurumu.Bekliyor,
            DenemeSayisi = 0,
        });
        await seedCtx.SaveChangesAsync();

        await using var workCtx = CreateDbContext();
        var claim = await new EBelgeOutboxClaimLeaseService(workCtx).TryClaimNextAsync(TimeSpan.FromSeconds(60));
        Assert.NotNull(claim);
        Assert.Equal(eBelgeKaydiId, claim!.EBelgeKaydiId);
        Assert.Equal(EBelgeOutboxIsTuru.UblImzala, claim.IsTuru);

        var imzalamaService = CreateService(workCtx);
        var handler = new EBelgeUblImzalaOutboxHandler(imzalamaService);
        var transitionService = new EBelgeOutboxLeaseTransitionService(workCtx);
        var retryPolicy = new EBelgeOutboxRetryPolicy();
        var islemeService = new EBelgeOutboxMesajIslemeService(
            [handler], retryPolicy, transitionService, NullLogger<EBelgeOutboxMesajIslemeService>.Instance);

        var sonuc = await islemeService.IsleAsync(claim);

        Assert.Equal(EBelgeOutboxIslemeSonucuTuru.Tamamlandi, sonuc.SonucTuru);

        await using var verifyCtx = CreateDbContext();
        var outbox = await verifyCtx.EBelgeOutboxMesajlari.AsNoTracking().SingleAsync(x => x.EBelgeKaydiId == eBelgeKaydiId && x.IsTuru == EBelgeOutboxIsTuru.UblImzala);
        Assert.Equal(EBelgeOutboxDurumu.Tamamlandi, outbox.Durum);
        Assert.True(await verifyCtx.EBelgeArtifactlari.AnyAsync(a => a.EBelgeKaydiId == eBelgeKaydiId && a.ArtifactAsamasi == EBelgeArtifactAsamasi.SignedReady));

        var kayit = await verifyCtx.EBelgeKayitlari.AsNoTracking().SingleAsync(x => x.Id == eBelgeKaydiId);
        Assert.Equal(EBelgeKaydiDurumu.SignedReady, kayit.Durum);
    }

    // ---- İdempotency (md.20) ----

    [IntegrationFact]
    public async Task AyniKaynagaEslesenMevcutSignedReadyIdempotentBasariylaTamamlanirIkinciSatirEklenmezVeYenidenDogrulanir()
    {
        await using var dbContext = CreateDbContext();
        var (eBelgeKaydiId, unsignedArtifact) = await SeedUnsignedArtifactAsync(dbContext);

        // GERÇEK imza motoruyla, servisin DIŞINDA, ÖNCEDEN üretilmiş GERÇEK bir imzalı sonuç -
        // "daha önce başarıyla tamamlanmış ama outbox mesajı her nedense yeniden işlenen" bir
        // idempotent-replay senaryosunu temsil eder (bkz. görev md.20).
        var imzalayici = new EBelgeXmlImzalayici(new EBelgeTestSertifikaSaglayici(), new EBelgeTestSertifikaGuvenPolicy());
        var onceImzaSonucu = await imzalayici.ImzalaAsync(new EBelgeXmlImzaTalebi
        {
            KurumId = _kurumId,
            UnsignedUblUtf8 = ImmutableArray.Create(unsignedArtifact.Icerik),
            UnsignedUblSha256 = unsignedArtifact.ArtifactSha256,
            RuleSetId = unsignedArtifact.RuleSetId,
            EBelgeUuid = Guid.NewGuid().ToString("D"),
            ImzalamaZamaniUtc = DateTime.UtcNow,
        }, CancellationToken.None);

        dbContext.EBelgeArtifactlari.Add(new EBelgeArtifact
        {
            KurumId = _kurumId,
            EBelgeKaydiId = eBelgeKaydiId,
            ArtifactTipi = EBelgeArtifactTipi.UblXml,
            ArtifactAsamasi = EBelgeArtifactAsamasi.SignedReady,
            RuleSetId = unsignedArtifact.RuleSetId,
            SnapshotSchemaVersion = unsignedArtifact.SnapshotSchemaVersion,
            KaynakSnapshotSha256 = unsignedArtifact.KaynakSnapshotSha256,
            ArtifactSha256 = onceImzaSonucu.SignedUblSha256,
            Icerik = onceImzaSonucu.SignedUblUtf8.ToArray(),
            MimeType = "application/xml",
            DosyaAdi = "onceden-imzali.xml",
            OlusturulmaZamaniUtc = DateTime.UtcNow,
            KaynakArtifactId = unsignedArtifact.Id,
            KaynakArtifactSha256 = unsignedArtifact.ArtifactSha256,
            ImzaProfili = onceImzaSonucu.ImzaProfili,
            ImzaAlgoritmasi = onceImzaSonucu.ImzaAlgoritmasi,
            DigestAlgoritmasi = onceImzaSonucu.DigestAlgoritmasi,
            ImzalayanSertifikaSha256ParmakIzi = onceImzaSonucu.SertifikaSha256ParmakIzi,
            ImzalamaZamaniUtc = onceImzaSonucu.ImzalamaZamaniUtc,
        });
        await dbContext.SaveChangesAsync();

        var claim = await SeedAndClaimUblImzalaOutboxAsync(dbContext, eBelgeKaydiId);
        var service = CreateService(dbContext);
        var sonuc = await service.ImzalaAsync(TalepFromClaim(claim, _kurumId, eBelgeKaydiId));

        Assert.NotNull(sonuc);
        Assert.True(sonuc!.BasariliMi, $"{sonuc.SonucTuru}: {sonuc.HataKodu} {sonuc.HataMesaji}");

        await using var verifyCtx = CreateDbContext();
        var sayi = await verifyCtx.EBelgeArtifactlari.CountAsync(a => a.EBelgeKaydiId == eBelgeKaydiId && a.ArtifactAsamasi == EBelgeArtifactAsamasi.SignedReady);
        Assert.Equal(1, sayi); // ikinci (kendi) satır EKLENMEDİ
        var signed = await verifyCtx.EBelgeArtifactlari.AsNoTracking().SingleAsync(a => a.EBelgeKaydiId == eBelgeKaydiId && a.ArtifactAsamasi == EBelgeArtifactAsamasi.SignedReady);
        Assert.Equal("onceden-imzali.xml", signed.DosyaAdi); // ORİJİNAL satır - üzerine YAZILMADI

        var outbox = await verifyCtx.EBelgeOutboxMesajlari.AsNoTracking().SingleAsync(x => x.Id == claim.OutboxMesajiId);
        Assert.Equal(EBelgeOutboxDurumu.Tamamlandi, outbox.Durum);
        var kayit = await verifyCtx.EBelgeKayitlari.AsNoTracking().SingleAsync(x => x.Id == eBelgeKaydiId);
        Assert.Equal(EBelgeKaydiDurumu.SignedReady, kayit.Durum);
    }

    [IntegrationFact]
    public async Task FarkliKaynagaBagliMevcutSignedReadyAtomikKaliciHataIdempotencyConflictUretir()
    {
        await using var dbContext = CreateDbContext();
        var (eBelgeKaydiId, unsignedArtifact) = await SeedUnsignedArtifactAsync(dbContext);

        // Kendine-referanslı FK (KaynakArtifactId->Id) GERÇEK bir satırı işaret etmelidir - bu
        // yüzden "yanlış kaynak" GERÇEK ama İLGİSİZ, İKİNCİ bir (ayrı EBelgeKaydi'ye bağlı) Unsigned
        // artefaktın Id'sidir (yalnızca sayısal olarak var olmayan bir Id KULLANILAMAZ - FK ihlaline
        // yol açar).
        var (_, ilgisizUnsignedArtifact) = await SeedUnsignedArtifactAsync(dbContext);

        // Aynı benzersiz anahtar (KurumId+EBelgeKaydiId+ArtifactTipi+ArtifactAsamasi) altında,
        // KASITLI OLARAK FARKLI bir kaynağa (Id/hash) bağlı "yabancı" bir SignedReady zaten var.
        dbContext.EBelgeArtifactlari.Add(new EBelgeArtifact
        {
            KurumId = _kurumId,
            EBelgeKaydiId = eBelgeKaydiId,
            ArtifactTipi = EBelgeArtifactTipi.UblXml,
            ArtifactAsamasi = EBelgeArtifactAsamasi.SignedReady,
            RuleSetId = "farkli-kural-seti",
            SnapshotSchemaVersion = unsignedArtifact.SnapshotSchemaVersion,
            KaynakSnapshotSha256 = unsignedArtifact.KaynakSnapshotSha256,
            ArtifactSha256 = new string('b', 64),
            Icerik = "<farkli/>"u8.ToArray(),
            MimeType = "application/xml",
            DosyaAdi = "yabanci-imzali.xml",
            OlusturulmaZamaniUtc = DateTime.UtcNow,
            KaynakArtifactId = ilgisizUnsignedArtifact.Id, // gerçek ama YANLIŞ/ilgisiz kaynak
            KaynakArtifactSha256 = ilgisizUnsignedArtifact.ArtifactSha256,
            ImzaProfili = "dummy",
            ImzaAlgoritmasi = "dummy",
            DigestAlgoritmasi = "dummy",
            ImzalayanSertifikaSha256ParmakIzi = "dummy",
            ImzalamaZamaniUtc = DateTime.UtcNow,
        });
        await dbContext.SaveChangesAsync();

        var claim = await SeedAndClaimUblImzalaOutboxAsync(dbContext, eBelgeKaydiId);
        var service = CreateService(dbContext);
        var sonuc = await service.ImzalaAsync(TalepFromClaim(claim, _kurumId, eBelgeKaydiId));

        Assert.NotNull(sonuc);
        Assert.Equal(EBelgeUblImzalamaSonucuTuru.AtomikKaliciHata, sonuc!.SonucTuru);
        Assert.Equal(EBelgeXmlImzaHataKodlari.SignedArtifactIdempotencyConflict, sonuc.HataKodu);

        await using var verifyCtx = CreateDbContext();
        var sayi = await verifyCtx.EBelgeArtifactlari.CountAsync(a => a.EBelgeKaydiId == eBelgeKaydiId && a.ArtifactAsamasi == EBelgeArtifactAsamasi.SignedReady);
        Assert.Equal(1, sayi); // rakip (yeni) satır EKLENMEDİ

        var kayit = await verifyCtx.EBelgeKayitlari.AsNoTracking().SingleAsync(x => x.Id == eBelgeKaydiId);
        Assert.Equal(EBelgeKaydiDurumu.ImzalamaKaliciHata, kayit.Durum);
        var outbox = await verifyCtx.EBelgeOutboxMesajlari.AsNoTracking().SingleAsync(x => x.Id == claim.OutboxMesajiId);
        Assert.Equal(EBelgeOutboxDurumu.Hata, outbox.Durum);
        Assert.Null(outbox.SonrakiDenemeZamaniUtc); // KALICI - retry ATANMADI
    }

    // ---- Kaynak (Unsigned) artefakt bütünlüğü (md.17 adım 5-7) ----

    [IntegrationFact]
    public async Task UnsignedArtifactYoksaAtomikKaliciHataOlur()
    {
        await using var dbContext = CreateDbContext();
        var satisBelgesiId = await CreateSatisBelgesiIdAsync(dbContext);
        var eBelgeKaydi = new EBelgeKaydi
        {
            KurumId = _kurumId,
            SatisBelgesiId = satisBelgesiId,
            EBelgeUuid = Guid.NewGuid().ToString("D"),
            EBelgeKanali = EBelgeKanali.EArsiv,
            Durum = EBelgeKaydiDurumu.UnsignedUblHazir,
        };
        dbContext.EBelgeKayitlari.Add(eBelgeKaydi);
        await dbContext.SaveChangesAsync();

        var claim = await SeedAndClaimUblImzalaOutboxAsync(dbContext, eBelgeKaydi.Id);
        var service = CreateService(dbContext);
        var sonuc = await service.ImzalaAsync(TalepFromClaim(claim, _kurumId, eBelgeKaydi.Id));

        Assert.NotNull(sonuc);
        Assert.Equal(EBelgeUblImzalamaSonucuTuru.AtomikKaliciHata, sonuc!.SonucTuru);
        Assert.Equal(EBelgeXmlImzaHataKodlari.UnsignedArtifactBulunamadi, sonuc.HataKodu);

        await using var verifyCtx = CreateDbContext();
        var kayit = await verifyCtx.EBelgeKayitlari.AsNoTracking().SingleAsync(x => x.Id == eBelgeKaydi.Id);
        Assert.Equal(EBelgeKaydiDurumu.ImzalamaKaliciHata, kayit.Durum);
    }

    [IntegrationFact]
    public async Task UnsignedArtifactSoftDeleteEdilmisseAtomikKaliciHataOlur()
    {
        await using var dbContext = CreateDbContext();
        var (eBelgeKaydiId, unsignedArtifact) = await SeedUnsignedArtifactAsync(dbContext);
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [muhasebe].[EBelgeArtifactlari] SET [IsDeleted] = 1 WHERE [Id] = {unsignedArtifact.Id}");

        var claim = await SeedAndClaimUblImzalaOutboxAsync(dbContext, eBelgeKaydiId);
        var service = CreateService(dbContext);
        var sonuc = await service.ImzalaAsync(TalepFromClaim(claim, _kurumId, eBelgeKaydiId));

        Assert.NotNull(sonuc);
        Assert.Equal(EBelgeUblImzalamaSonucuTuru.AtomikKaliciHata, sonuc!.SonucTuru);
        Assert.Equal(EBelgeXmlImzaHataKodlari.UnsignedArtifactSoftDeleted, sonuc.HataKodu);

        await using var verifyCtx = CreateDbContext();
        Assert.False(await verifyCtx.EBelgeArtifactlari.AnyAsync(a => a.EBelgeKaydiId == eBelgeKaydiId && a.ArtifactAsamasi == EBelgeArtifactAsamasi.SignedReady));
    }

    [IntegrationFact]
    public async Task UnsignedArtifactSaklananIcerikHashiKayitliHashIleUyusmuyorsaAtomikKaliciHataOlur()
    {
        await using var dbContext = CreateDbContext();
        var (eBelgeKaydiId, unsignedArtifact) = await SeedUnsignedArtifactAsync(dbContext);

        // İçerik, kayıtlı ArtifactSha256'yı ARTIK karşılamayacak şekilde bozulur (ör. depolama
        // katmanında sessiz bir bozulma senaryosunu temsil eder) - md.7/md.17 adım 7.
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [muhasebe].[EBelgeArtifactlari] SET [Icerik] = 0x00 WHERE [Id] = {unsignedArtifact.Id}");

        var claim = await SeedAndClaimUblImzalaOutboxAsync(dbContext, eBelgeKaydiId);
        var service = CreateService(dbContext);
        var sonuc = await service.ImzalaAsync(TalepFromClaim(claim, _kurumId, eBelgeKaydiId));

        Assert.NotNull(sonuc);
        Assert.Equal(EBelgeUblImzalamaSonucuTuru.AtomikKaliciHata, sonuc!.SonucTuru);
        Assert.Equal(EBelgeXmlImzaHataKodlari.KaynakHashUyumsuz, sonuc.HataKodu);
    }

    // ---- Lease sahipliği (md.17 adım 4, 12-13) ----

    [IntegrationFact]
    public async Task LeaseSuresiImzalamaSirasindaDolmussaSignedReadyOlusmazVeKayitDegismez()
    {
        await using var dbContext = CreateDbContext();
        var (eBelgeKaydiId, _) = await SeedUnsignedArtifactAsync(dbContext);
        var claim = await SeedAndClaimUblImzalaOutboxAsync(dbContext, eBelgeKaydiId);

        // İmzalama+doğrulama+XSD+Schematron'ın (DB dışı, uzun sürebilen) bölümü sırasında lease'in
        // dolduğunu simüle eder - bkz. Faz 2B.6.1 ile AYNI desen.
        await BackdateLeaseExpiryAsync(dbContext, claim.OutboxMesajiId);

        var service = CreateService(dbContext);
        var sonuc = await service.ImzalaAsync(TalepFromClaim(claim, _kurumId, eBelgeKaydiId));

        Assert.Equal(EBelgeUblImzalamaSonucuTuru.SahiplikKaybedildi, sonuc!.SonucTuru);

        await using var verifyCtx = CreateDbContext();
        Assert.False(await verifyCtx.EBelgeArtifactlari.AnyAsync(a => a.EBelgeKaydiId == eBelgeKaydiId && a.ArtifactAsamasi == EBelgeArtifactAsamasi.SignedReady));
        var kayit = await verifyCtx.EBelgeKayitlari.AsNoTracking().SingleAsync(x => x.Id == eBelgeKaydiId);
        Assert.Equal(EBelgeKaydiDurumu.UnsignedUblHazir, kayit.Durum); // DEĞİŞMEDİ
        var outbox = await verifyCtx.EBelgeOutboxMesajlari.AsNoTracking().SingleAsync(x => x.Id == claim.OutboxMesajiId);
        Assert.Equal(EBelgeOutboxDurumu.Isleniyor, outbox.Durum);
    }

    [IntegrationFact]
    public async Task YanlisIsTuruTasiyanClaimIleImzalamaYapilamaz()
    {
        // Faz 2B.7 görev md.16 - iş-türü-farkında ownership guard'ının (IsOwnedForJobAsync)
        // İMZALAMA servisi TARAFINDAN da gerçekten kullanıldığını doğrular: ArtefaktOlustur
        // türünde GERÇEK bir claim, imzalama talebinde KULLANILMAYA ÇALIŞILIR.
        await using var dbContext = CreateDbContext();
        var (eBelgeKaydiId, _) = await SeedUnsignedArtifactAsync(dbContext);

        dbContext.EBelgeOutboxMesajlari.Add(new EBelgeOutboxMesaji
        {
            KurumId = _kurumId,
            EBelgeKaydiId = eBelgeKaydiId,
            IsTuru = EBelgeOutboxIsTuru.ArtefaktOlustur,
            Durum = EBelgeOutboxDurumu.Bekliyor,
            DenemeSayisi = 0,
        });
        await dbContext.SaveChangesAsync();

        var claim = await new EBelgeOutboxClaimLeaseService(dbContext).TryClaimNextAsync(TimeSpan.FromMinutes(5));
        Assert.NotNull(claim);
        Assert.Equal(EBelgeOutboxIsTuru.ArtefaktOlustur, claim!.IsTuru);

        var service = CreateService(dbContext);
        var sonuc = await service.ImzalaAsync(TalepFromClaim(claim, _kurumId, eBelgeKaydiId));

        Assert.Equal(EBelgeUblImzalamaSonucuTuru.SahiplikKaybedildi, sonuc!.SonucTuru);

        await using var verifyCtx = CreateDbContext();
        Assert.False(await verifyCtx.EBelgeArtifactlari.AnyAsync(a => a.EBelgeKaydiId == eBelgeKaydiId && a.ArtifactAsamasi == EBelgeArtifactAsamasi.SignedReady));
        var outbox = await verifyCtx.EBelgeOutboxMesajlari.AsNoTracking().SingleAsync(x => x.Id == claim.OutboxMesajiId);
        Assert.Equal(EBelgeOutboxDurumu.Isleniyor, outbox.Durum); // yanlış-türlü claim terminalize EDİLMEDİ
    }
}
