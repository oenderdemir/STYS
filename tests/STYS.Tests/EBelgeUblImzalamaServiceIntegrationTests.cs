using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Xml;
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
[Trait("Domain", "EBelge")]
[Trait("TestLevel", "CryptoIntegration")]
[Trait("Dependency", "SqlServer")]
[Trait("Dependency", "JavaSidecar")]
[Trait("Dependency", "Cryptography")]
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

        // Faz 2B.10.1 görev md.1 - claim SQL'i artık immutable karar + GÜNCEL/aktif kurum
        // politikasını ZORUNLU kılar; bu dosya EBelgeKaydi'yi SatisBelgesiService'in normal
        // akışını KULLANMADAN doğrudan seed ettiğinden, eşlik eden karar burada seed edilir.
        await EBelgeKurumPolitikaTestSupport.SeedEBelgeKarariAsync(dbContext, _kurumId, satisBelgesiId, eBelgeKaydi.Id);

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

        var claimService = new EBelgeOutboxClaimLeaseService(dbContext, EBelgeTestSigningActivationGate.Acik);
        var claim = await claimService.TryClaimNextAsync(leaseDuration ?? TimeSpan.FromMinutes(5));
        Assert.NotNull(claim);
        return claim!;
    }

    private static EBelgeUblImzalamaTalebi TalepFromClaim(EBelgeOutboxClaimLeaseResultDto claim, int kurumId, int eBelgeKaydiId)
        => new(kurumId, eBelgeKaydiId, claim.OutboxMesajiId, claim.KilitToken, claim.KilitBitisZamaniUtc);

    private static Task BackdateLeaseExpiryAsync(StysAppDbContext dbContext, int outboxMesajiId) =>
        dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [muhasebe].[EBelgeOutboxMesajlari] SET [KilitBitisZamaniUtc] = DATEADD(MINUTE, -1, SYSUTCDATETIME()) WHERE [Id] = {outboxMesajiId}");

    private EBelgeUblImzalamaService CreateService(StysAppDbContext dbContext, TimeProvider? timeProvider = null, IEBelgeSigningActivationGate? signingActivationGate = null)
    {
        if (_sidecarFixture.BaseUrl is null)
        {
            Assert.Fail($"Sidecar ayağa kaldırılamadı: {_sidecarFixture.AtlamaNedeni}");
        }

        var kuralSeti = EBelgeUblRendererTestVerisi.KuralSetiYukle();
        var xsdValidator = new EBelgeUblXsdValidator(kuralSeti);
        var http = new HttpClient { BaseAddress = new Uri(_sidecarFixture.BaseUrl!), Timeout = TimeSpan.FromSeconds(15) };
        var schematronValidator = new SaxonSidecarEBelgeSchematronValidator(http);

        var tp = timeProvider ?? TimeProvider.System;
        return new EBelgeUblImzalamaService(
            dbContext,
            new EBelgeXmlImzalayici(new EBelgeTestSertifikaSaglayici(), new EBelgeTestSertifikaGuvenPolicy()),
            new EBelgeXmlImzaDogrulayici(),
            xsdValidator,
            schematronValidator,
            new EBelgeOutboxLeaseTransitionService(dbContext),
            EBelgeKurumPolitikaTestSupport.CreateAlwaysAktifServisi(dbContext, tp),
            new EBelgeKurumPolitikaTransactionGuard(dbContext),
            signingActivationGate ?? EBelgeTestSigningActivationGate.Acik,
            tp,
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
    [Trait("CriticalInvariant", "SignedExactByteHash")]
    public async Task GecerliImzaTamAtomikBasariylaSignedReadyArtefaktUretirVeHashZinciriDogrulanir()
    {
        await using var dbContext = CreateDbContext();
        var (eBelgeKaydiId, unsignedArtifact) = await SeedUnsignedArtifactAsync(dbContext);
        var claim = await SeedAndClaimUblImzalaOutboxAsync(dbContext, eBelgeKaydiId);

        // Faz 2B.9 - ÖNCEDEN sabit bir takvim tarihi (2026-08-05) kullanılıyordu; test sertifikası
        // (EBelgeTestSertifikaSaglayici) İSE varsayılan olarak GERÇEK duvar saatine göre
        // (`UtcNow.AddDays(-1)`) notBefore alır - takvim GERÇEKTEN o tarihi geçtiğinde sabit
        // değer sertifikanın notBefore'undan ÖNCEYE düşüp "henüz geçerlilik tarihine ulaşmadı"
        // hatasıyla ZAMAN İÇİNDE kaçınılmaz biçimde bozulan bir test üretiyordu (flaky/time-bomb -
        // bkz. docs/e-belge-test-stratejisi.md "Flaky test politikası"). Testin AMACI belirli bir
        // takvim tarihini doğrulamak DEĞİL, imzalama zamanının `TimeProvider`'dan GELDİĞİNİ ve
        // AYNEN saklandığını kanıtlamaktır - bu yüzden artık test ÇALIŞTIĞI ANIN GERÇEK zamanı
        // kullanılır (sertifikanın notBefore/notAfter penceresiyle HER ZAMAN uyumludur).
        var sabitZaman = DateTimeOffset.UtcNow;
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
        var claim = await new EBelgeOutboxClaimLeaseService(workCtx, EBelgeTestSigningActivationGate.Acik).TryClaimNextAsync(TimeSpan.FromSeconds(60));
        Assert.NotNull(claim);
        Assert.Equal(eBelgeKaydiId, claim!.EBelgeKaydiId);
        Assert.Equal(EBelgeOutboxIsTuru.UblImzala, claim.IsTuru);

        var imzalamaService = CreateService(workCtx);
        var handler = new EBelgeUblImzalaOutboxHandler(imzalamaService);
        var transitionService = new EBelgeOutboxLeaseTransitionService(workCtx);
        var retryPolicy = new EBelgeOutboxRetryPolicy();
        var islemeService = new EBelgeOutboxMesajIslemeService(
            [handler], retryPolicy, transitionService,
            EBelgeKurumPolitikaTestSupport.CreateAlwaysAktifServisi(workCtx),
            NullLogger<EBelgeOutboxMesajIslemeService>.Instance);

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

    /// <summary>GERÇEK imza motoruyla, servisin DIŞINDA, unsignedArtifact'e TAM eşleşen (KaynakArtifactId/Hash) GERÇEK bir SignedReady artefaktı üretir ve kalıcılaştırır - idempotent-replay senaryolarını temsil eder (bkz. görev md.20).</summary>
    private async Task<EBelgeArtifact> SeedMatchingSignedReadyAsync(StysAppDbContext dbContext, int eBelgeKaydiId, EBelgeArtifact unsignedArtifact, string dosyaAdi = "onceden-imzali.xml")
    {
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

        var signed = new EBelgeArtifact
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
            DosyaAdi = dosyaAdi,
            OlusturulmaZamaniUtc = DateTime.UtcNow,
            KaynakArtifactId = unsignedArtifact.Id,
            KaynakArtifactSha256 = unsignedArtifact.ArtifactSha256,
            ImzaProfili = onceImzaSonucu.ImzaProfili,
            ImzaAlgoritmasi = onceImzaSonucu.ImzaAlgoritmasi,
            DigestAlgoritmasi = onceImzaSonucu.DigestAlgoritmasi,
            ImzalayanSertifikaSha256ParmakIzi = onceImzaSonucu.SertifikaSha256ParmakIzi,
            ImzalamaZamaniUtc = onceImzaSonucu.ImzalamaZamaniUtc,
        };
        dbContext.EBelgeArtifactlari.Add(signed);
        await dbContext.SaveChangesAsync();
        return signed;
    }

    private sealed class AlwaysFailingXsdValidator : IEBelgeUblXsdValidator
    {
        public void Validate(ImmutableArray<byte> xmlBytes)
            => throw new EBelgeUblXsdValidationFailedException(["test: kasıtlı XSD hatası"]);

        public void ValidateUnsignedRendererOutput(ImmutableArray<byte> xmlBytes)
            => throw new NotSupportedException();
    }

    private sealed class AlwaysInvalidSchematronValidator : IEBelgeSchematronValidator
    {
        public Task<EBelgeSchematronValidationResult> ValidateAsync(ImmutableArray<byte> xmlUtf8, string ruleSetId, CancellationToken cancellationToken)
            => Task.FromResult(new EBelgeSchematronValidationResult(false, [new EBelgeSchematronViolation("TEST-001", "/Invoice", "test: kasıtlı ihlal", "fatal")]));
    }

    /// <summary>
    /// Gerçek schematron sonucunu AYNEN döndürür, ama BUNU YAPARKEN - Faz 2B.7.1 görev md.5'in
    /// "tx-dışı doğrulama sırasında SQL transaction/UPDLOCK TUTULMAZ" gereksinimini KANITLAMAK
    /// için - AYRI bir bağlantı üzerinden, KISA bir komut zaman aşımıyla, AYNI outbox satırını
    /// UPDLOCK ile okumayı DENER. Eğer çağıran taraf o satırda GERÇEKTEN bir kilit TUTUYOR
    /// olsaydı, bu deneme ZAMAN AŞIMINA UĞRARDI (bloklanırdı) - başarılı tamamlanması, kilidin
    /// TUTULMADIĞININ doğrudan kanıtıdır.
    /// </summary>
    private sealed class TransactionProbeSchematronDecorator : IEBelgeSchematronValidator
    {
        private readonly IEBelgeSchematronValidator _inner;
        private readonly int _outboxMesajiId;

        public bool ProbeBasarili { get; private set; }

        public TransactionProbeSchematronDecorator(IEBelgeSchematronValidator inner, int outboxMesajiId)
        {
            _inner = inner;
            _outboxMesajiId = outboxMesajiId;
        }

        public async Task<EBelgeSchematronValidationResult> ValidateAsync(ImmutableArray<byte> xmlUtf8, string ruleSetId, CancellationToken cancellationToken)
        {
            var sonuc = await _inner.ValidateAsync(xmlUtf8, ruleSetId, cancellationToken);

            await using var probeCtx = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
            await using var connection = new Microsoft.Data.SqlClient.SqlConnection(probeCtx.Database.GetConnectionString());
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandTimeout = 3;
            command.CommandText = "SELECT TOP (1) [Id] FROM [muhasebe].[EBelgeOutboxMesajlari] WITH (UPDLOCK, ROWLOCK) WHERE [Id] = @id";
            command.Parameters.AddWithValue("@id", _outboxMesajiId);
            await command.ExecuteScalarAsync(cancellationToken);

            ProbeBasarili = true;
            return sonuc;
        }
    }

    /// <summary>
    /// Gerçek schematron sonucunu döndürdükten SONRA, AYRI bir bağlantı üzerinden, verilen
    /// SignedReady artefaktını soft-delete eder - "tx-dışı doğrulama SIRASINDA artefakt
    /// değişti/kayboldu" yarışını DETERMİNİSTİK biçimde simüle eder (bkz. görev md.20, senaryo 20).
    /// </summary>
    private sealed class RowSoftDeletingSchematronDecorator : IEBelgeSchematronValidator
    {
        private readonly IEBelgeSchematronValidator _inner;
        private readonly long _signedArtifactId;

        public RowSoftDeletingSchematronDecorator(IEBelgeSchematronValidator inner, long signedArtifactId)
        {
            _inner = inner;
            _signedArtifactId = signedArtifactId;
        }

        public async Task<EBelgeSchematronValidationResult> ValidateAsync(ImmutableArray<byte> xmlUtf8, string ruleSetId, CancellationToken cancellationToken)
        {
            var sonuc = await _inner.ValidateAsync(xmlUtf8, ruleSetId, cancellationToken);

            await using var sideCtx = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
            await sideCtx.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE [muhasebe].[EBelgeArtifactlari] SET [IsDeleted] = 1 WHERE [Id] = {_signedArtifactId}", cancellationToken);

            return sonuc;
        }
    }

    [IntegrationFact]
    public async Task MevcutSignedReadyXsdGecersizsePartOfIdempotentBasariOlmazKaliciHataUretir()
    {
        await using var dbContext = CreateDbContext();
        var (eBelgeKaydiId, unsignedArtifact) = await SeedUnsignedArtifactAsync(dbContext);
        await SeedMatchingSignedReadyAsync(dbContext, eBelgeKaydiId, unsignedArtifact);
        var claim = await SeedAndClaimUblImzalaOutboxAsync(dbContext, eBelgeKaydiId);

        if (_sidecarFixture.BaseUrl is null)
        {
            Assert.Fail($"Sidecar ayağa kaldırılamadı: {_sidecarFixture.AtlamaNedeni}");
        }

        var http = new HttpClient { BaseAddress = new Uri(_sidecarFixture.BaseUrl!), Timeout = TimeSpan.FromSeconds(15) };
        var service = new EBelgeUblImzalamaService(
            dbContext,
            new EBelgeXmlImzalayici(new EBelgeTestSertifikaSaglayici(), new EBelgeTestSertifikaGuvenPolicy()),
            new EBelgeXmlImzaDogrulayici(),
            new AlwaysFailingXsdValidator(),
            new SaxonSidecarEBelgeSchematronValidator(http),
            new EBelgeOutboxLeaseTransitionService(dbContext),
            EBelgeKurumPolitikaTestSupport.CreateAlwaysAktifServisi(dbContext),
            new EBelgeKurumPolitikaTransactionGuard(dbContext),
            EBelgeTestSigningActivationGate.Acik,
            TimeProvider.System,
            NullLogger<EBelgeUblImzalamaService>.Instance);

        var sonuc = await service.ImzalaAsync(TalepFromClaim(claim, _kurumId, eBelgeKaydiId));

        Assert.NotNull(sonuc);
        Assert.Equal(EBelgeUblImzalamaSonucuTuru.AtomikKaliciHata, sonuc!.SonucTuru);
        Assert.Equal(EBelgeXmlImzaHataKodlari.SignedArtifactIdempotencyConflict, sonuc.HataKodu);

        await using var verifyCtx = CreateDbContext();
        var kayit = await verifyCtx.EBelgeKayitlari.AsNoTracking().SingleAsync(x => x.Id == eBelgeKaydiId);
        Assert.Equal(EBelgeKaydiDurumu.ImzalamaKaliciHata, kayit.Durum);
        var sayi = await verifyCtx.EBelgeArtifactlari.CountAsync(a => a.EBelgeKaydiId == eBelgeKaydiId && a.ArtifactAsamasi == EBelgeArtifactAsamasi.SignedReady);
        Assert.Equal(1, sayi); // mevcut satır DEĞİŞMEDİ, yeni satır EKLENMEDİ
    }

    [IntegrationFact]
    public async Task MevcutSignedReadySchematronIhlaliVarsaIdempotentBasariOlmazKaliciHataUretir()
    {
        await using var dbContext = CreateDbContext();
        var (eBelgeKaydiId, unsignedArtifact) = await SeedUnsignedArtifactAsync(dbContext);
        await SeedMatchingSignedReadyAsync(dbContext, eBelgeKaydiId, unsignedArtifact);
        var claim = await SeedAndClaimUblImzalaOutboxAsync(dbContext, eBelgeKaydiId);

        var kuralSeti = EBelgeUblRendererTestVerisi.KuralSetiYukle();
        var service = new EBelgeUblImzalamaService(
            dbContext,
            new EBelgeXmlImzalayici(new EBelgeTestSertifikaSaglayici(), new EBelgeTestSertifikaGuvenPolicy()),
            new EBelgeXmlImzaDogrulayici(),
            new EBelgeUblXsdValidator(kuralSeti),
            new AlwaysInvalidSchematronValidator(),
            new EBelgeOutboxLeaseTransitionService(dbContext),
            EBelgeKurumPolitikaTestSupport.CreateAlwaysAktifServisi(dbContext),
            new EBelgeKurumPolitikaTransactionGuard(dbContext),
            EBelgeTestSigningActivationGate.Acik,
            TimeProvider.System,
            NullLogger<EBelgeUblImzalamaService>.Instance);

        var sonuc = await service.ImzalaAsync(TalepFromClaim(claim, _kurumId, eBelgeKaydiId));

        Assert.NotNull(sonuc);
        Assert.Equal(EBelgeUblImzalamaSonucuTuru.AtomikKaliciHata, sonuc!.SonucTuru);
        Assert.Equal(EBelgeXmlImzaHataKodlari.SignedArtifactIdempotencyConflict, sonuc.HataKodu);

        await using var verifyCtx = CreateDbContext();
        var kayit = await verifyCtx.EBelgeKayitlari.AsNoTracking().SingleAsync(x => x.Id == eBelgeKaydiId);
        Assert.Equal(EBelgeKaydiDurumu.ImzalamaKaliciHata, kayit.Durum);
        var sayi = await verifyCtx.EBelgeArtifactlari.CountAsync(a => a.EBelgeKaydiId == eBelgeKaydiId && a.ArtifactAsamasi == EBelgeArtifactAsamasi.SignedReady);
        Assert.Equal(1, sayi);
    }

    [IntegrationFact]
    public async Task MevcutSignedReadyDogrulamasiSirasindaSqlTransactionAcikDegildirVeOutboxSatiriKilitliDegildir()
    {
        await using var dbContext = CreateDbContext();
        var (eBelgeKaydiId, unsignedArtifact) = await SeedUnsignedArtifactAsync(dbContext);
        await SeedMatchingSignedReadyAsync(dbContext, eBelgeKaydiId, unsignedArtifact);
        var claim = await SeedAndClaimUblImzalaOutboxAsync(dbContext, eBelgeKaydiId);

        if (_sidecarFixture.BaseUrl is null)
        {
            Assert.Fail($"Sidecar ayağa kaldırılamadı: {_sidecarFixture.AtlamaNedeni}");
        }

        var http = new HttpClient { BaseAddress = new Uri(_sidecarFixture.BaseUrl!), Timeout = TimeSpan.FromSeconds(15) };
        var probe = new TransactionProbeSchematronDecorator(new SaxonSidecarEBelgeSchematronValidator(http), claim.OutboxMesajiId);

        var service = new EBelgeUblImzalamaService(
            dbContext,
            new EBelgeXmlImzalayici(new EBelgeTestSertifikaSaglayici(), new EBelgeTestSertifikaGuvenPolicy()),
            new EBelgeXmlImzaDogrulayici(),
            new EBelgeUblXsdValidator(EBelgeUblRendererTestVerisi.KuralSetiYukle()),
            probe,
            new EBelgeOutboxLeaseTransitionService(dbContext),
            EBelgeKurumPolitikaTestSupport.CreateAlwaysAktifServisi(dbContext),
            new EBelgeKurumPolitikaTransactionGuard(dbContext),
            EBelgeTestSigningActivationGate.Acik,
            TimeProvider.System,
            NullLogger<EBelgeUblImzalamaService>.Instance);

        var sonuc = await service.ImzalaAsync(TalepFromClaim(claim, _kurumId, eBelgeKaydiId));

        Assert.NotNull(sonuc);
        Assert.True(sonuc!.BasariliMi, $"{sonuc.SonucTuru}: {sonuc.HataKodu} {sonuc.HataMesaji}");
        Assert.True(probe.ProbeBasarili, "Schematron çağrısı sırasında outbox satırı UPDLOCK ile kilitli tutuluyor gibi görünüyor (probe hiç tamamlanamadı).");
    }

    [IntegrationFact]
    public async Task MevcutSignedReadyTxDisiDogrulamaSirasindaDegisirseSonucKullanilmazGeciciHataDoner()
    {
        await using var dbContext = CreateDbContext();
        var (eBelgeKaydiId, unsignedArtifact) = await SeedUnsignedArtifactAsync(dbContext);
        var mevcutSigned = await SeedMatchingSignedReadyAsync(dbContext, eBelgeKaydiId, unsignedArtifact);
        var claim = await SeedAndClaimUblImzalaOutboxAsync(dbContext, eBelgeKaydiId);

        if (_sidecarFixture.BaseUrl is null)
        {
            Assert.Fail($"Sidecar ayağa kaldırılamadı: {_sidecarFixture.AtlamaNedeni}");
        }

        var http = new HttpClient { BaseAddress = new Uri(_sidecarFixture.BaseUrl!), Timeout = TimeSpan.FromSeconds(15) };
        var rowChanger = new RowSoftDeletingSchematronDecorator(new SaxonSidecarEBelgeSchematronValidator(http), mevcutSigned.Id);

        var service = new EBelgeUblImzalamaService(
            dbContext,
            new EBelgeXmlImzalayici(new EBelgeTestSertifikaSaglayici(), new EBelgeTestSertifikaGuvenPolicy()),
            new EBelgeXmlImzaDogrulayici(),
            new EBelgeUblXsdValidator(EBelgeUblRendererTestVerisi.KuralSetiYukle()),
            rowChanger,
            new EBelgeOutboxLeaseTransitionService(dbContext),
            EBelgeKurumPolitikaTestSupport.CreateAlwaysAktifServisi(dbContext),
            new EBelgeKurumPolitikaTransactionGuard(dbContext),
            EBelgeTestSigningActivationGate.Acik,
            TimeProvider.System,
            NullLogger<EBelgeUblImzalamaService>.Instance);

        var sonuc = await service.ImzalaAsync(TalepFromClaim(claim, _kurumId, eBelgeKaydiId));

        Assert.NotNull(sonuc);
        Assert.Equal(EBelgeUblImzalamaSonucuTuru.GeciciHata, sonuc!.SonucTuru);
        Assert.Equal("EBELGE_SIGNING_YARIS_DURUMU", sonuc.HataKodu);

        await using var verifyCtx = CreateDbContext();
        var kayit = await verifyCtx.EBelgeKayitlari.AsNoTracking().SingleAsync(x => x.Id == eBelgeKaydiId);
        Assert.Equal(EBelgeKaydiDurumu.UnsignedUblHazir, kayit.Durum); // geçici hata EBelgeKaydi'yı DEĞİŞTİRMEZ
        var outbox = await verifyCtx.EBelgeOutboxMesajlari.AsNoTracking().SingleAsync(x => x.Id == claim.OutboxMesajiId);
        Assert.Equal(EBelgeOutboxDurumu.Isleniyor, outbox.Durum); // terminalize EDİLMEDİ
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
            // Faz 2B.7.2 md.3'teki YENİ tam-bayt (exact-byte) hash ön-kontrolünün bu testte
            // ERKEN devreye girip asıl test edilmek istenen idempotency-conflict yolunu MASKELEMEMESİ
            // için ArtifactSha256, Icerik'in GERÇEK SHA-256'sı olmalıdır (kasıtlı farklılık yalnızca
            // KaynakArtifactId/KaynakArtifactSha256 alanlarında olmalı).
            ArtifactSha256 = Convert.ToHexString(SHA256.HashData("<farkli/>"u8.ToArray())),
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
        await EBelgeKurumPolitikaTestSupport.SeedEBelgeKarariAsync(dbContext, _kurumId, satisBelgesiId, eBelgeKaydi.Id);

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

        var claim = await new EBelgeOutboxClaimLeaseService(dbContext, EBelgeTestSigningActivationGate.Acik).TryClaimNextAsync(TimeSpan.FromMinutes(5));
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

    // ---- Faz 2B.7.2: bozuk girdi sınıflandırması, mevcut/kaynak hash bütünlüğü, tx-penceresi yarışları ----

    private sealed class ThrowingXmlExceptionDogrulayici : IEBelgeXmlImzaDogrulayici
    {
        public Task<EBelgeXmlImzaDogrulamaSonucu> DogrulaAsync(ImmutableArray<byte> signedXmlUtf8, CancellationToken cancellationToken)
            => throw new XmlException("test: kasıtlı beklenmedik XmlException (savunma derinliği senaryosu - bkz. görev md.2).");
    }

    [IntegrationFact]
    public async Task YeniImzaBagimsizDogrulamaBeklenmedikBozukSonucUretirseAtomikKaliciHataBozukImzaBelgesiOlurGeciciDegil()
    {
        await using var dbContext = CreateDbContext();
        var (eBelgeKaydiId, _) = await SeedUnsignedArtifactAsync(dbContext);
        var claim = await SeedAndClaimUblImzalaOutboxAsync(dbContext, eBelgeKaydiId);

        var kuralSeti = EBelgeUblRendererTestVerisi.KuralSetiYukle();
        var service = new EBelgeUblImzalamaService(
            dbContext,
            new EBelgeXmlImzalayici(new EBelgeTestSertifikaSaglayici(), new EBelgeTestSertifikaGuvenPolicy()),
            new ThrowingXmlExceptionDogrulayici(),
            new EBelgeUblXsdValidator(kuralSeti),
            new AlwaysInvalidSchematronValidator(), // dogrulama erken başarısız olduğundan asla çağrılmamalı
            new EBelgeOutboxLeaseTransitionService(dbContext),
            EBelgeKurumPolitikaTestSupport.CreateAlwaysAktifServisi(dbContext),
            new EBelgeKurumPolitikaTransactionGuard(dbContext),
            EBelgeTestSigningActivationGate.Acik,
            TimeProvider.System,
            NullLogger<EBelgeUblImzalamaService>.Instance);

        var sonuc = await service.ImzalaAsync(TalepFromClaim(claim, _kurumId, eBelgeKaydiId));

        Assert.NotNull(sonuc);
        Assert.Equal(EBelgeUblImzalamaSonucuTuru.AtomikKaliciHata, sonuc!.SonucTuru); // GEÇİCİ hata OLARAK sızmadı
        Assert.Equal(EBelgeXmlImzaHataKodlari.BozukImzaBelgesi, sonuc.HataKodu);
        Assert.DoesNotContain("<", sonuc.HataMesaji); // kişisel veri/XML/sertifika içeriği SIZDIRMAYAN sabit mesaj

        await using var verifyCtx = CreateDbContext();
        var kayit = await verifyCtx.EBelgeKayitlari.AsNoTracking().SingleAsync(x => x.Id == eBelgeKaydiId);
        Assert.Equal(EBelgeKaydiDurumu.ImzalamaKaliciHata, kayit.Durum);
        var outbox = await verifyCtx.EBelgeOutboxMesajlari.AsNoTracking().SingleAsync(x => x.Id == claim.OutboxMesajiId);
        Assert.Equal(EBelgeOutboxDurumu.Hata, outbox.Durum);
        Assert.Null(outbox.SonrakiDenemeZamaniUtc); // KALICI - retry ATANMADI
        Assert.False(await verifyCtx.EBelgeArtifactlari.AnyAsync(a => a.EBelgeKaydiId == eBelgeKaydiId && a.ArtifactAsamasi == EBelgeArtifactAsamasi.SignedReady));
    }

    private sealed class CallCountingDogrulayici : IEBelgeXmlImzaDogrulayici
    {
        private readonly IEBelgeXmlImzaDogrulayici _inner;
        public int CagriSayisi { get; private set; }

        public CallCountingDogrulayici(IEBelgeXmlImzaDogrulayici inner) => _inner = inner;

        public async Task<EBelgeXmlImzaDogrulamaSonucu> DogrulaAsync(ImmutableArray<byte> signedXmlUtf8, CancellationToken cancellationToken)
        {
            CagriSayisi++;
            return await _inner.DogrulaAsync(signedXmlUtf8, cancellationToken);
        }
    }

    [IntegrationFact]
    public async Task MevcutSignedIcerigiTamperlenirseImzaDogrulamasiAtlanirAtomikKaliciHataMevcutArtifactHashUyumsuzOlur()
    {
        await using var dbContext = CreateDbContext();
        var (eBelgeKaydiId, unsignedArtifact) = await SeedUnsignedArtifactAsync(dbContext);
        var mevcutSigned = await SeedMatchingSignedReadyAsync(dbContext, eBelgeKaydiId, unsignedArtifact);

        // İçerik SESSİZCE bozulur (ör. depolama katmanı bozulması) - kayıtlı ArtifactSha256 sütunu
        // KASITLI OLARAK GÜNCELLENMEZ (bkz. görev md.3/md.9 - "hash sütunu aynı bırakılırsa
        // idempotent başarı olmamalı").
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [muhasebe].[EBelgeArtifactlari] SET [Icerik] = 0x00 WHERE [Id] = {mevcutSigned.Id}");

        var claim = await SeedAndClaimUblImzalaOutboxAsync(dbContext, eBelgeKaydiId);

        var spyDogrulayici = new CallCountingDogrulayici(new EBelgeXmlImzaDogrulayici());
        var kuralSeti = EBelgeUblRendererTestVerisi.KuralSetiYukle();
        var service = new EBelgeUblImzalamaService(
            dbContext,
            new EBelgeXmlImzalayici(new EBelgeTestSertifikaSaglayici(), new EBelgeTestSertifikaGuvenPolicy()),
            spyDogrulayici,
            new EBelgeUblXsdValidator(kuralSeti),
            new AlwaysInvalidSchematronValidator(), // hash uyuşmazlığında asla çağrılmamalı
            new EBelgeOutboxLeaseTransitionService(dbContext),
            EBelgeKurumPolitikaTestSupport.CreateAlwaysAktifServisi(dbContext),
            new EBelgeKurumPolitikaTransactionGuard(dbContext),
            EBelgeTestSigningActivationGate.Acik,
            TimeProvider.System,
            NullLogger<EBelgeUblImzalamaService>.Instance);

        var sonuc = await service.ImzalaAsync(TalepFromClaim(claim, _kurumId, eBelgeKaydiId));

        Assert.NotNull(sonuc);
        Assert.Equal(EBelgeUblImzalamaSonucuTuru.AtomikKaliciHata, sonuc!.SonucTuru);
        Assert.Equal(EBelgeXmlImzaHataKodlari.MevcutArtifactHashUyumsuz, sonuc.HataKodu);
        Assert.Equal(0, spyDogrulayici.CagriSayisi); // hash uyuşmazlığında imza doğrulamasına HİÇ gidilmedi (md.8)

        await using var verifyCtx = CreateDbContext();
        var kayit = await verifyCtx.EBelgeKayitlari.AsNoTracking().SingleAsync(x => x.Id == eBelgeKaydiId);
        Assert.Equal(EBelgeKaydiDurumu.ImzalamaKaliciHata, kayit.Durum);
    }

    /// <summary>
    /// Gerçek schematron sonucunu döndürdükten SONRA, AYRI bir bağlantı üzerinden, verilen
    /// SignedReady satırını BAŞKA (ama kendi içinde GEÇERLİ) bir imzalı içerikle DEĞİŞTİRİR - "yalnız
    /// hash SÜTUNUNU değil, satırın TAMAMINI/İçeriği tekrar karşılaştırma" gerekliliğini KANITLAMAK
    /// için (bkz. Faz 2B.7.2 görev md.4/md.10).
    /// </summary>
    private sealed class RowContentSwappingSchematronDecorator : IEBelgeSchematronValidator
    {
        private readonly IEBelgeSchematronValidator _inner;
        private readonly long _signedArtifactId;
        private readonly byte[] _yeniIcerik;
        private readonly string _yeniHash;

        public RowContentSwappingSchematronDecorator(IEBelgeSchematronValidator inner, long signedArtifactId, byte[] yeniIcerik, string yeniHash)
        {
            _inner = inner;
            _signedArtifactId = signedArtifactId;
            _yeniIcerik = yeniIcerik;
            _yeniHash = yeniHash;
        }

        public async Task<EBelgeSchematronValidationResult> ValidateAsync(ImmutableArray<byte> xmlUtf8, string ruleSetId, CancellationToken cancellationToken)
        {
            var sonuc = await _inner.ValidateAsync(xmlUtf8, ruleSetId, cancellationToken);

            await using var sideCtx = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
            await sideCtx.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE [muhasebe].[EBelgeArtifactlari] SET [Icerik] = {_yeniIcerik}, [ArtifactSha256] = {_yeniHash} WHERE [Id] = {_signedArtifactId}", cancellationToken);

            return sonuc;
        }
    }

    [IntegrationFact]
    public async Task MevcutSignedTxDisiDogrulamaSonrasiIcerikFarkliGecerliImzayaDegistirilirseYarisDurumuDoner()
    {
        await using var dbContext = CreateDbContext();
        var (eBelgeKaydiId, unsignedArtifact) = await SeedUnsignedArtifactAsync(dbContext);
        var mevcutSigned = await SeedMatchingSignedReadyAsync(dbContext, eBelgeKaydiId, unsignedArtifact);
        var claim = await SeedAndClaimUblImzalaOutboxAsync(dbContext, eBelgeKaydiId);

        if (_sidecarFixture.BaseUrl is null)
        {
            Assert.Fail($"Sidecar ayağa kaldırılamadı: {_sidecarFixture.AtlamaNedeni}");
        }

        // Farklı (ama kendi içinde GEÇERLİ) bir ikinci imza üretilir - "içerik başka bir GEÇERLİ
        // imzayla DEĞİŞTİRİLDİ" yarışını temsil eder.
        var imzalayici = new EBelgeXmlImzalayici(new EBelgeTestSertifikaSaglayici(), new EBelgeTestSertifikaGuvenPolicy());
        var ikinciImza = await imzalayici.ImzalaAsync(new EBelgeXmlImzaTalebi
        {
            KurumId = _kurumId,
            UnsignedUblUtf8 = ImmutableArray.Create(unsignedArtifact.Icerik),
            UnsignedUblSha256 = unsignedArtifact.ArtifactSha256,
            RuleSetId = unsignedArtifact.RuleSetId,
            EBelgeUuid = Guid.NewGuid().ToString("D"),
            ImzalamaZamaniUtc = DateTime.UtcNow.AddSeconds(5),
        }, CancellationToken.None);

        var http = new HttpClient { BaseAddress = new Uri(_sidecarFixture.BaseUrl!), Timeout = TimeSpan.FromSeconds(15) };
        var swapper = new RowContentSwappingSchematronDecorator(
            new SaxonSidecarEBelgeSchematronValidator(http), mevcutSigned.Id, ikinciImza.SignedUblUtf8.ToArray(), ikinciImza.SignedUblSha256);

        var service = new EBelgeUblImzalamaService(
            dbContext,
            new EBelgeXmlImzalayici(new EBelgeTestSertifikaSaglayici(), new EBelgeTestSertifikaGuvenPolicy()),
            new EBelgeXmlImzaDogrulayici(),
            new EBelgeUblXsdValidator(EBelgeUblRendererTestVerisi.KuralSetiYukle()),
            swapper,
            new EBelgeOutboxLeaseTransitionService(dbContext),
            EBelgeKurumPolitikaTestSupport.CreateAlwaysAktifServisi(dbContext),
            new EBelgeKurumPolitikaTransactionGuard(dbContext),
            EBelgeTestSigningActivationGate.Acik,
            TimeProvider.System,
            NullLogger<EBelgeUblImzalamaService>.Instance);

        var sonuc = await service.ImzalaAsync(TalepFromClaim(claim, _kurumId, eBelgeKaydiId));

        Assert.NotNull(sonuc);
        Assert.Equal(EBelgeUblImzalamaSonucuTuru.GeciciHata, sonuc!.SonucTuru);
        Assert.Equal(EBelgeXmlImzaHataKodlari.YarisDurumu, sonuc.HataKodu);

        await using var verifyCtx = CreateDbContext();
        var kayit = await verifyCtx.EBelgeKayitlari.AsNoTracking().SingleAsync(x => x.Id == eBelgeKaydiId);
        Assert.Equal(EBelgeKaydiDurumu.UnsignedUblHazir, kayit.Durum); // geçici hata DEĞİŞTİRMEZ
    }

    /// <summary>Gerçek schematron sonucunu döndürdükten SONRA, verilen Unsigned kaynak artefaktını soft-delete eder.</summary>
    private sealed class UnsignedSoftDeletingSchematronDecorator : IEBelgeSchematronValidator
    {
        private readonly IEBelgeSchematronValidator _inner;
        private readonly long _unsignedArtifactId;

        public UnsignedSoftDeletingSchematronDecorator(IEBelgeSchematronValidator inner, long unsignedArtifactId)
        {
            _inner = inner;
            _unsignedArtifactId = unsignedArtifactId;
        }

        public async Task<EBelgeSchematronValidationResult> ValidateAsync(ImmutableArray<byte> xmlUtf8, string ruleSetId, CancellationToken cancellationToken)
        {
            var sonuc = await _inner.ValidateAsync(xmlUtf8, ruleSetId, cancellationToken);

            await using var sideCtx = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
            await sideCtx.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE [muhasebe].[EBelgeArtifactlari] SET [IsDeleted] = 1 WHERE [Id] = {_unsignedArtifactId}", cancellationToken);

            return sonuc;
        }
    }

    [IntegrationFact]
    public async Task YeniImzaSirasindaUnsignedKaynakSoftDeleteEdilirseGeciciHataKaynakImzalamaSirasindaDegistiDonerVeSignedReadyEklenmez()
    {
        await using var dbContext = CreateDbContext();
        var (eBelgeKaydiId, unsignedArtifact) = await SeedUnsignedArtifactAsync(dbContext);
        var claim = await SeedAndClaimUblImzalaOutboxAsync(dbContext, eBelgeKaydiId);

        if (_sidecarFixture.BaseUrl is null)
        {
            Assert.Fail($"Sidecar ayağa kaldırılamadı: {_sidecarFixture.AtlamaNedeni}");
        }

        var http = new HttpClient { BaseAddress = new Uri(_sidecarFixture.BaseUrl!), Timeout = TimeSpan.FromSeconds(15) };
        // Kaynak, YENİ imzanın Schematron adımı İLE Faz-3'ün KISA transaction'ı ARASINDAKİ
        // pencerede değişir - Faz 2B.7.2 md.5'in bu penceredeki yeniden-doğrulamayı KISA
        // transaction İÇİNDE yaptığını (md.13) DOLAYLI olarak kanıtlar: değişiklik ANCAK
        // transaction içindeki OkuUnsignedKilitliAsync tarafından yakalanabilir.
        var softDeleter = new UnsignedSoftDeletingSchematronDecorator(new SaxonSidecarEBelgeSchematronValidator(http), unsignedArtifact.Id);

        var service = new EBelgeUblImzalamaService(
            dbContext,
            new EBelgeXmlImzalayici(new EBelgeTestSertifikaSaglayici(), new EBelgeTestSertifikaGuvenPolicy()),
            new EBelgeXmlImzaDogrulayici(),
            new EBelgeUblXsdValidator(EBelgeUblRendererTestVerisi.KuralSetiYukle()),
            softDeleter,
            new EBelgeOutboxLeaseTransitionService(dbContext),
            EBelgeKurumPolitikaTestSupport.CreateAlwaysAktifServisi(dbContext),
            new EBelgeKurumPolitikaTransactionGuard(dbContext),
            EBelgeTestSigningActivationGate.Acik,
            TimeProvider.System,
            NullLogger<EBelgeUblImzalamaService>.Instance);

        var sonuc = await service.ImzalaAsync(TalepFromClaim(claim, _kurumId, eBelgeKaydiId));

        Assert.NotNull(sonuc);
        Assert.Equal(EBelgeUblImzalamaSonucuTuru.GeciciHata, sonuc!.SonucTuru);
        Assert.Equal(EBelgeXmlImzaHataKodlari.KaynakImzalamaSirasindaDegisti, sonuc.HataKodu);

        await using var verifyCtx = CreateDbContext();
        Assert.False(await verifyCtx.EBelgeArtifactlari.AnyAsync(a => a.EBelgeKaydiId == eBelgeKaydiId && a.ArtifactAsamasi == EBelgeArtifactAsamasi.SignedReady));
        var kayit = await verifyCtx.EBelgeKayitlari.AsNoTracking().SingleAsync(x => x.Id == eBelgeKaydiId);
        Assert.Equal(EBelgeKaydiDurumu.UnsignedUblHazir, kayit.Durum); // geçici hata DEĞİŞTİRMEZ
        var outbox = await verifyCtx.EBelgeOutboxMesajlari.AsNoTracking().SingleAsync(x => x.Id == claim.OutboxMesajiId);
        Assert.Equal(EBelgeOutboxDurumu.Isleniyor, outbox.Durum); // terminalize EDİLMEDİ
    }

    /// <summary>Gerçek schematron sonucunu döndürdükten SONRA, verilen Unsigned kaynak artefaktının İçeriğini (ArtifactSha256 sütununu GÜNCELLEMEDEN) bozar.</summary>
    private sealed class UnsignedContentCorruptingSchematronDecorator : IEBelgeSchematronValidator
    {
        private readonly IEBelgeSchematronValidator _inner;
        private readonly long _unsignedArtifactId;

        public UnsignedContentCorruptingSchematronDecorator(IEBelgeSchematronValidator inner, long unsignedArtifactId)
        {
            _inner = inner;
            _unsignedArtifactId = unsignedArtifactId;
        }

        public async Task<EBelgeSchematronValidationResult> ValidateAsync(ImmutableArray<byte> xmlUtf8, string ruleSetId, CancellationToken cancellationToken)
        {
            var sonuc = await _inner.ValidateAsync(xmlUtf8, ruleSetId, cancellationToken);

            await using var sideCtx = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
            await sideCtx.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE [muhasebe].[EBelgeArtifactlari] SET [Icerik] = 0x00 WHERE [Id] = {_unsignedArtifactId}", cancellationToken);

            return sonuc;
        }
    }

    [IntegrationFact]
    public async Task YeniImzaSirasindaUnsignedKaynakIcerigiBozulursaAtomikKaliciHataKaynakHashUyumsuzOlur()
    {
        await using var dbContext = CreateDbContext();
        var (eBelgeKaydiId, unsignedArtifact) = await SeedUnsignedArtifactAsync(dbContext);
        var claim = await SeedAndClaimUblImzalaOutboxAsync(dbContext, eBelgeKaydiId);

        if (_sidecarFixture.BaseUrl is null)
        {
            Assert.Fail($"Sidecar ayağa kaldırılamadı: {_sidecarFixture.AtlamaNedeni}");
        }

        var http = new HttpClient { BaseAddress = new Uri(_sidecarFixture.BaseUrl!), Timeout = TimeSpan.FromSeconds(15) };
        var corrupter = new UnsignedContentCorruptingSchematronDecorator(new SaxonSidecarEBelgeSchematronValidator(http), unsignedArtifact.Id);

        var service = new EBelgeUblImzalamaService(
            dbContext,
            new EBelgeXmlImzalayici(new EBelgeTestSertifikaSaglayici(), new EBelgeTestSertifikaGuvenPolicy()),
            new EBelgeXmlImzaDogrulayici(),
            new EBelgeUblXsdValidator(EBelgeUblRendererTestVerisi.KuralSetiYukle()),
            corrupter,
            new EBelgeOutboxLeaseTransitionService(dbContext),
            EBelgeKurumPolitikaTestSupport.CreateAlwaysAktifServisi(dbContext),
            new EBelgeKurumPolitikaTransactionGuard(dbContext),
            EBelgeTestSigningActivationGate.Acik,
            TimeProvider.System,
            NullLogger<EBelgeUblImzalamaService>.Instance);

        var sonuc = await service.ImzalaAsync(TalepFromClaim(claim, _kurumId, eBelgeKaydiId));

        Assert.NotNull(sonuc);
        Assert.Equal(EBelgeUblImzalamaSonucuTuru.AtomikKaliciHata, sonuc!.SonucTuru);
        Assert.Equal(EBelgeXmlImzaHataKodlari.KaynakHashUyumsuz, sonuc.HataKodu);

        await using var verifyCtx = CreateDbContext();
        Assert.False(await verifyCtx.EBelgeArtifactlari.AnyAsync(a => a.EBelgeKaydiId == eBelgeKaydiId && a.ArtifactAsamasi == EBelgeArtifactAsamasi.SignedReady));
        var kayit = await verifyCtx.EBelgeKayitlari.AsNoTracking().SingleAsync(x => x.Id == eBelgeKaydiId);
        Assert.Equal(EBelgeKaydiDurumu.ImzalamaKaliciHata, kayit.Durum);
    }

    private sealed class CountingSchematronDecorator : IEBelgeSchematronValidator
    {
        private readonly IEBelgeSchematronValidator _inner;
        public int CagriSayisi { get; private set; }

        public CountingSchematronDecorator(IEBelgeSchematronValidator inner) => _inner = inner;

        public async Task<EBelgeSchematronValidationResult> ValidateAsync(ImmutableArray<byte> xmlUtf8, string ruleSetId, CancellationToken cancellationToken)
        {
            CagriSayisi++;
            return await _inner.ValidateAsync(xmlUtf8, ruleSetId, cancellationToken);
        }
    }

    [IntegrationFact]
    public async Task YeniSignedInsertAkisindaSchematronTamOlarakBirKezCagrilirKisaTransactionAltindaTekrarCalismaz()
    {
        await using var dbContext = CreateDbContext();
        var (eBelgeKaydiId, _) = await SeedUnsignedArtifactAsync(dbContext);
        var claim = await SeedAndClaimUblImzalaOutboxAsync(dbContext, eBelgeKaydiId);

        if (_sidecarFixture.BaseUrl is null)
        {
            Assert.Fail($"Sidecar ayağa kaldırılamadı: {_sidecarFixture.AtlamaNedeni}");
        }

        var http = new HttpClient { BaseAddress = new Uri(_sidecarFixture.BaseUrl!), Timeout = TimeSpan.FromSeconds(15) };
        var counter = new CountingSchematronDecorator(new SaxonSidecarEBelgeSchematronValidator(http));

        var service = new EBelgeUblImzalamaService(
            dbContext,
            new EBelgeXmlImzalayici(new EBelgeTestSertifikaSaglayici(), new EBelgeTestSertifikaGuvenPolicy()),
            new EBelgeXmlImzaDogrulayici(),
            new EBelgeUblXsdValidator(EBelgeUblRendererTestVerisi.KuralSetiYukle()),
            counter,
            new EBelgeOutboxLeaseTransitionService(dbContext),
            EBelgeKurumPolitikaTestSupport.CreateAlwaysAktifServisi(dbContext),
            new EBelgeKurumPolitikaTransactionGuard(dbContext),
            EBelgeTestSigningActivationGate.Acik,
            TimeProvider.System,
            NullLogger<EBelgeUblImzalamaService>.Instance);

        var sonuc = await service.ImzalaAsync(TalepFromClaim(claim, _kurumId, eBelgeKaydiId));

        Assert.NotNull(sonuc);
        Assert.True(sonuc!.BasariliMi, $"{sonuc.SonucTuru}: {sonuc.HataKodu} {sonuc.HataMesaji}");
        // Faz 2B.7.2 md.5'teki YENİ kaynak yeniden-doğrulaması (OkuUnsignedKilitliAsync), Schematron'u
        // TEKRAR ÇAĞIRMAZ - yalnız SQL satır okuma+hash karşılaştırmasıdır. Bu, Schematron'un TÜM
        // akış boyunca yalnız BİR KEZ (Faz 2'de, tx açılmadan ÖNCE) çalıştığını - kısa Faz-3
        // transaction'ı ALTINDA sidecar/kriptografi işi YAPILMADIĞINI (md.14) - kanıtlar.
        Assert.Equal(1, counter.CagriSayisi);
    }

    [IntegrationFact]
    public async Task UnsignedKaynakImzalamaSirasindaDegistiktenSonraYeniClaimIleYenidenDenemeBasariliOlur()
    {
        await using var dbContext = CreateDbContext();
        var (eBelgeKaydiId, unsignedArtifact) = await SeedUnsignedArtifactAsync(dbContext);
        var claim = await SeedAndClaimUblImzalaOutboxAsync(dbContext, eBelgeKaydiId);

        if (_sidecarFixture.BaseUrl is null)
        {
            Assert.Fail($"Sidecar ayağa kaldırılamadı: {_sidecarFixture.AtlamaNedeni}");
        }

        var http = new HttpClient { BaseAddress = new Uri(_sidecarFixture.BaseUrl!), Timeout = TimeSpan.FromSeconds(15) };
        var softDeleter = new UnsignedSoftDeletingSchematronDecorator(new SaxonSidecarEBelgeSchematronValidator(http), unsignedArtifact.Id);

        var ilkService = new EBelgeUblImzalamaService(
            dbContext,
            new EBelgeXmlImzalayici(new EBelgeTestSertifikaSaglayici(), new EBelgeTestSertifikaGuvenPolicy()),
            new EBelgeXmlImzaDogrulayici(),
            new EBelgeUblXsdValidator(EBelgeUblRendererTestVerisi.KuralSetiYukle()),
            softDeleter,
            new EBelgeOutboxLeaseTransitionService(dbContext),
            EBelgeKurumPolitikaTestSupport.CreateAlwaysAktifServisi(dbContext),
            new EBelgeKurumPolitikaTransactionGuard(dbContext),
            EBelgeTestSigningActivationGate.Acik,
            TimeProvider.System,
            NullLogger<EBelgeUblImzalamaService>.Instance);

        var ilkSonuc = await ilkService.ImzalaAsync(TalepFromClaim(claim, _kurumId, eBelgeKaydiId));
        Assert.NotNull(ilkSonuc);
        Assert.Equal(EBelgeUblImzalamaSonucuTuru.GeciciHata, ilkSonuc!.SonucTuru);

        // Kaynak, yarış SIRASINDA soft-delete edildi - GERÇEK bir kalıcı bozulma DEĞİL, "geri al"
        // (undo) edilir. Outbox mesajı terminalize EDİLMEDİĞİ için AYNI mesaj YENİ bir lease ile
        // (retry policy'nin gerçekte yapacağı gibi) yeniden claim edilebilir ve BAŞARIYLA
        // tamamlanabilir olmalıdır (bkz. görev md.5, "GEÇİCİ bir yarış durumudur, yeni bir claim
        // ile yeniden imzalanmalıdır").
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [muhasebe].[EBelgeArtifactlari] SET [IsDeleted] = 0 WHERE [Id] = {unsignedArtifact.Id}");
        await BackdateLeaseExpiryAsync(dbContext, claim.OutboxMesajiId);

        var yeniClaim = await new EBelgeOutboxClaimLeaseService(dbContext, EBelgeTestSigningActivationGate.Acik).TryClaimNextAsync(TimeSpan.FromMinutes(5));
        Assert.NotNull(yeniClaim);

        var ikinciService = CreateService(dbContext);
        var ikinciSonuc = await ikinciService.ImzalaAsync(TalepFromClaim(yeniClaim!, _kurumId, eBelgeKaydiId));

        Assert.NotNull(ikinciSonuc);
        Assert.True(ikinciSonuc!.BasariliMi, $"{ikinciSonuc.SonucTuru}: {ikinciSonuc.HataKodu} {ikinciSonuc.HataMesaji}");

        await using var verifyCtx = CreateDbContext();
        Assert.True(await verifyCtx.EBelgeArtifactlari.AnyAsync(a => a.EBelgeKaydiId == eBelgeKaydiId && a.ArtifactAsamasi == EBelgeArtifactAsamasi.SignedReady));
        var kayit = await verifyCtx.EBelgeKayitlari.AsNoTracking().SingleAsync(x => x.Id == eBelgeKaydiId);
        Assert.Equal(EBelgeKaydiDurumu.SignedReady, kayit.Durum);
    }

    // ---- Faz 2B.7.3: kaynak artifact kimliği (ID-bağlı) ve imza metadata bütünlüğü ----

    /// <summary>
    /// Gerçek schematron sonucunu döndürdükten SONRA, AYRI bir bağlantı üzerinden, verilen
    /// delegate'i çalıştırır - Faz 2B.7.3'ün çeşitli "tx-dışı imzalama/doğrulama SIRASINDA bir alan
    /// değişti" yarış senaryolarını (Unsigned VEYA SignedReady satırı üzerinde) TEK bir genel
    /// decorator ile simüle etmek için (bkz. Faz 2B.7.2'deki tekil-amaçlı decorator'lerin
    /// GENELLEŞTİRİLMİŞ hali).
    /// </summary>
    private sealed class FieldMutatingSchematronDecorator : IEBelgeSchematronValidator
    {
        private readonly IEBelgeSchematronValidator _inner;
        private readonly Func<StysAppDbContext, CancellationToken, Task> _mutateAsync;

        public FieldMutatingSchematronDecorator(IEBelgeSchematronValidator inner, Func<StysAppDbContext, CancellationToken, Task> mutateAsync)
        {
            _inner = inner;
            _mutateAsync = mutateAsync;
        }

        public async Task<EBelgeSchematronValidationResult> ValidateAsync(ImmutableArray<byte> xmlUtf8, string ruleSetId, CancellationToken cancellationToken)
        {
            var sonuc = await _inner.ValidateAsync(xmlUtf8, ruleSetId, cancellationToken);

            await using var sideCtx = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
            await _mutateAsync(sideCtx, cancellationToken);

            return sonuc;
        }
    }

    /// <summary>
    /// Gerçek schematron sonucunu döndürdükten SONRA, verilen Unsigned satırı FİZİKSEL olarak
    /// siler (soft-delete DEĞİL) ve AYNI iş anahtarıyla (KurumId+EBelgeKaydiId+ArtifactTipi+
    /// ArtifactAsamasi=Unsigned) YENİ, FARKLI bir Id'ye sahip bir satır ekler - "kaynak, imzalama
    /// sırasında fiziksel olarak silinip aynı anahtarla yeni ID'li satırla değiştirildi" senaryosunu
    /// (bkz. Faz 2B.7.3 test senaryosu 1) DETERMİNİSTİK biçimde simüle eder. Fiziksel silme
    /// GEREKİR - benzersizlik indeksi IsDeleted'e göre FİLTRELENMEDİĞİNDEN, yalnız soft-delete
    /// aynı anahtarla yeni bir satır eklenmesine ZATEN İZİN VERMEZDİ.
    /// </summary>
    private sealed class UnsignedPhysicallyReplacingSchematronDecorator : IEBelgeSchematronValidator
    {
        private readonly IEBelgeSchematronValidator _inner;
        private readonly long _eskiUnsignedId;
        private readonly int _kurumId;
        private readonly int _eBelgeKaydiId;
        private readonly string _ruleSetId;
        private readonly int _snapshotSchemaVersion;
        private readonly string _kaynakSnapshotSha256;

        public long? YeniUnsignedId { get; private set; }

        public UnsignedPhysicallyReplacingSchematronDecorator(
            IEBelgeSchematronValidator inner, long eskiUnsignedId, int kurumId, int eBelgeKaydiId,
            string ruleSetId, int snapshotSchemaVersion, string kaynakSnapshotSha256)
        {
            _inner = inner;
            _eskiUnsignedId = eskiUnsignedId;
            _kurumId = kurumId;
            _eBelgeKaydiId = eBelgeKaydiId;
            _ruleSetId = ruleSetId;
            _snapshotSchemaVersion = snapshotSchemaVersion;
            _kaynakSnapshotSha256 = kaynakSnapshotSha256;
        }

        public async Task<EBelgeSchematronValidationResult> ValidateAsync(ImmutableArray<byte> xmlUtf8, string ruleSetId, CancellationToken cancellationToken)
        {
            var sonuc = await _inner.ValidateAsync(xmlUtf8, ruleSetId, cancellationToken);

            await using var sideCtx = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
            await sideCtx.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM [muhasebe].[EBelgeArtifactlari] WHERE [Id] = {_eskiUnsignedId}", cancellationToken);

            var yeniIcerik = "<degistirilmis-unsigned-fiziksel-replace/>"u8.ToArray();
            var yeniSatir = new EBelgeArtifact
            {
                KurumId = _kurumId,
                EBelgeKaydiId = _eBelgeKaydiId,
                ArtifactTipi = EBelgeArtifactTipi.UblXml,
                ArtifactAsamasi = EBelgeArtifactAsamasi.Unsigned,
                RuleSetId = _ruleSetId,
                SnapshotSchemaVersion = _snapshotSchemaVersion,
                KaynakSnapshotSha256 = _kaynakSnapshotSha256,
                ArtifactSha256 = Convert.ToHexString(SHA256.HashData(yeniIcerik)),
                Icerik = yeniIcerik,
                MimeType = "application/xml",
                DosyaAdi = "yeni-fiziksel-unsigned.xml",
                OlusturulmaZamaniUtc = DateTime.UtcNow,
            };
            sideCtx.EBelgeArtifactlari.Add(yeniSatir);
            await sideCtx.SaveChangesAsync(cancellationToken);
            YeniUnsignedId = yeniSatir.Id;

            return sonuc;
        }
    }

    [IntegrationFact]
    public async Task YeniImzaSirasindaUnsignedFizikselSilinipAyniAnahtarlaYeniIdliSatirEklenirseSignedReadyOlusmazTypeSafeSonucUretir()
    {
        await using var dbContext = CreateDbContext();
        var (eBelgeKaydiId, unsignedArtifact) = await SeedUnsignedArtifactAsync(dbContext);
        var claim = await SeedAndClaimUblImzalaOutboxAsync(dbContext, eBelgeKaydiId);

        if (_sidecarFixture.BaseUrl is null)
        {
            Assert.Fail($"Sidecar ayağa kaldırılamadı: {_sidecarFixture.AtlamaNedeni}");
        }

        var http = new HttpClient { BaseAddress = new Uri(_sidecarFixture.BaseUrl!), Timeout = TimeSpan.FromSeconds(15) };
        var replacer = new UnsignedPhysicallyReplacingSchematronDecorator(
            new SaxonSidecarEBelgeSchematronValidator(http), unsignedArtifact.Id, _kurumId, eBelgeKaydiId,
            unsignedArtifact.RuleSetId, unsignedArtifact.SnapshotSchemaVersion, unsignedArtifact.KaynakSnapshotSha256);

        var service = new EBelgeUblImzalamaService(
            dbContext,
            new EBelgeXmlImzalayici(new EBelgeTestSertifikaSaglayici(), new EBelgeTestSertifikaGuvenPolicy()),
            new EBelgeXmlImzaDogrulayici(),
            new EBelgeUblXsdValidator(EBelgeUblRendererTestVerisi.KuralSetiYukle()),
            replacer,
            new EBelgeOutboxLeaseTransitionService(dbContext),
            EBelgeKurumPolitikaTestSupport.CreateAlwaysAktifServisi(dbContext),
            new EBelgeKurumPolitikaTransactionGuard(dbContext),
            EBelgeTestSigningActivationGate.Acik,
            TimeProvider.System,
            NullLogger<EBelgeUblImzalamaService>.Instance);

        var sonuc = await service.ImzalaAsync(TalepFromClaim(claim, _kurumId, eBelgeKaydiId));

        Assert.NotNull(sonuc);
        // Type-safe sonuç - generic bir FK ihlali/DbUpdateException'a DÜŞÜLMEDİĞİNİN kanıtı: kilitli
        // yeniden okuma (tam Id ile) `null` döner, servis bunu NORMAL bir GeciciHata olarak
        // sınıflandırır (exception fırlatılıp yutulmaz/yakalanmadan patlamaz).
        Assert.Equal(EBelgeUblImzalamaSonucuTuru.GeciciHata, sonuc!.SonucTuru);
        Assert.Equal(EBelgeXmlImzaHataKodlari.KaynakImzalamaSirasindaDegisti, sonuc.HataKodu);

        await using var verifyCtx = CreateDbContext();
        Assert.False(await verifyCtx.EBelgeArtifactlari.AnyAsync(a => a.EBelgeKaydiId == eBelgeKaydiId && a.ArtifactAsamasi == EBelgeArtifactAsamasi.SignedReady));
        Assert.True(await verifyCtx.EBelgeArtifactlari.AnyAsync(a => a.Id == replacer.YeniUnsignedId));
    }

    [IntegrationFact]
    public async Task YeniImzaSirasindaUnsignedRuleSetIdDegisirseSignedReadyOlusmazGeciciHataDoner()
    {
        await using var dbContext = CreateDbContext();
        var (eBelgeKaydiId, unsignedArtifact) = await SeedUnsignedArtifactAsync(dbContext);
        var claim = await SeedAndClaimUblImzalaOutboxAsync(dbContext, eBelgeKaydiId);

        if (_sidecarFixture.BaseUrl is null)
        {
            Assert.Fail($"Sidecar ayağa kaldırılamadı: {_sidecarFixture.AtlamaNedeni}");
        }

        var http = new HttpClient { BaseAddress = new Uri(_sidecarFixture.BaseUrl!), Timeout = TimeSpan.FromSeconds(15) };
        var mutator = new FieldMutatingSchematronDecorator(
            new SaxonSidecarEBelgeSchematronValidator(http),
            (ctx, ct) => ctx.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE [muhasebe].[EBelgeArtifactlari] SET [RuleSetId] = {"degistirilmis-kural-seti"} WHERE [Id] = {unsignedArtifact.Id}", ct));

        var service = new EBelgeUblImzalamaService(
            dbContext,
            new EBelgeXmlImzalayici(new EBelgeTestSertifikaSaglayici(), new EBelgeTestSertifikaGuvenPolicy()),
            new EBelgeXmlImzaDogrulayici(),
            new EBelgeUblXsdValidator(EBelgeUblRendererTestVerisi.KuralSetiYukle()),
            mutator,
            new EBelgeOutboxLeaseTransitionService(dbContext),
            EBelgeKurumPolitikaTestSupport.CreateAlwaysAktifServisi(dbContext),
            new EBelgeKurumPolitikaTransactionGuard(dbContext),
            EBelgeTestSigningActivationGate.Acik,
            TimeProvider.System,
            NullLogger<EBelgeUblImzalamaService>.Instance);

        var sonuc = await service.ImzalaAsync(TalepFromClaim(claim, _kurumId, eBelgeKaydiId));

        Assert.NotNull(sonuc);
        Assert.Equal(EBelgeUblImzalamaSonucuTuru.GeciciHata, sonuc!.SonucTuru);
        Assert.Equal(EBelgeXmlImzaHataKodlari.KaynakImzalamaSirasindaDegisti, sonuc.HataKodu);

        await using var verifyCtx = CreateDbContext();
        Assert.False(await verifyCtx.EBelgeArtifactlari.AnyAsync(a => a.EBelgeKaydiId == eBelgeKaydiId && a.ArtifactAsamasi == EBelgeArtifactAsamasi.SignedReady));
    }

    [IntegrationFact]
    public async Task YeniImzaSirasindaUnsignedSnapshotSchemaVersionDegisirseSignedReadyOlusmazGeciciHataDoner()
    {
        await using var dbContext = CreateDbContext();
        var (eBelgeKaydiId, unsignedArtifact) = await SeedUnsignedArtifactAsync(dbContext);
        var claim = await SeedAndClaimUblImzalaOutboxAsync(dbContext, eBelgeKaydiId);

        if (_sidecarFixture.BaseUrl is null)
        {
            Assert.Fail($"Sidecar ayağa kaldırılamadı: {_sidecarFixture.AtlamaNedeni}");
        }

        var http = new HttpClient { BaseAddress = new Uri(_sidecarFixture.BaseUrl!), Timeout = TimeSpan.FromSeconds(15) };
        var mutator = new FieldMutatingSchematronDecorator(
            new SaxonSidecarEBelgeSchematronValidator(http),
            (ctx, ct) => ctx.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE [muhasebe].[EBelgeArtifactlari] SET [SnapshotSchemaVersion] = {unsignedArtifact.SnapshotSchemaVersion + 1} WHERE [Id] = {unsignedArtifact.Id}", ct));

        var service = new EBelgeUblImzalamaService(
            dbContext,
            new EBelgeXmlImzalayici(new EBelgeTestSertifikaSaglayici(), new EBelgeTestSertifikaGuvenPolicy()),
            new EBelgeXmlImzaDogrulayici(),
            new EBelgeUblXsdValidator(EBelgeUblRendererTestVerisi.KuralSetiYukle()),
            mutator,
            new EBelgeOutboxLeaseTransitionService(dbContext),
            EBelgeKurumPolitikaTestSupport.CreateAlwaysAktifServisi(dbContext),
            new EBelgeKurumPolitikaTransactionGuard(dbContext),
            EBelgeTestSigningActivationGate.Acik,
            TimeProvider.System,
            NullLogger<EBelgeUblImzalamaService>.Instance);

        var sonuc = await service.ImzalaAsync(TalepFromClaim(claim, _kurumId, eBelgeKaydiId));

        Assert.NotNull(sonuc);
        Assert.Equal(EBelgeUblImzalamaSonucuTuru.GeciciHata, sonuc!.SonucTuru);
        Assert.Equal(EBelgeXmlImzaHataKodlari.KaynakImzalamaSirasindaDegisti, sonuc.HataKodu);

        await using var verifyCtx = CreateDbContext();
        Assert.False(await verifyCtx.EBelgeArtifactlari.AnyAsync(a => a.EBelgeKaydiId == eBelgeKaydiId && a.ArtifactAsamasi == EBelgeArtifactAsamasi.SignedReady));
    }

    [IntegrationFact]
    public async Task YeniImzaSirasindaUnsignedKaynakSnapshotSha256DegisirseSignedReadyOlusmazGeciciHataDoner()
    {
        await using var dbContext = CreateDbContext();
        var (eBelgeKaydiId, unsignedArtifact) = await SeedUnsignedArtifactAsync(dbContext);
        var claim = await SeedAndClaimUblImzalaOutboxAsync(dbContext, eBelgeKaydiId);

        if (_sidecarFixture.BaseUrl is null)
        {
            Assert.Fail($"Sidecar ayağa kaldırılamadı: {_sidecarFixture.AtlamaNedeni}");
        }

        var http = new HttpClient { BaseAddress = new Uri(_sidecarFixture.BaseUrl!), Timeout = TimeSpan.FromSeconds(15) };
        var mutator = new FieldMutatingSchematronDecorator(
            new SaxonSidecarEBelgeSchematronValidator(http),
            (ctx, ct) => ctx.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE [muhasebe].[EBelgeArtifactlari] SET [KaynakSnapshotSha256] = {new string('c', 64)} WHERE [Id] = {unsignedArtifact.Id}", ct));

        var service = new EBelgeUblImzalamaService(
            dbContext,
            new EBelgeXmlImzalayici(new EBelgeTestSertifikaSaglayici(), new EBelgeTestSertifikaGuvenPolicy()),
            new EBelgeXmlImzaDogrulayici(),
            new EBelgeUblXsdValidator(EBelgeUblRendererTestVerisi.KuralSetiYukle()),
            mutator,
            new EBelgeOutboxLeaseTransitionService(dbContext),
            EBelgeKurumPolitikaTestSupport.CreateAlwaysAktifServisi(dbContext),
            new EBelgeKurumPolitikaTransactionGuard(dbContext),
            EBelgeTestSigningActivationGate.Acik,
            TimeProvider.System,
            NullLogger<EBelgeUblImzalamaService>.Instance);

        var sonuc = await service.ImzalaAsync(TalepFromClaim(claim, _kurumId, eBelgeKaydiId));

        Assert.NotNull(sonuc);
        Assert.Equal(EBelgeUblImzalamaSonucuTuru.GeciciHata, sonuc!.SonucTuru);
        Assert.Equal(EBelgeXmlImzaHataKodlari.KaynakImzalamaSirasindaDegisti, sonuc.HataKodu);

        await using var verifyCtx = CreateDbContext();
        Assert.False(await verifyCtx.EBelgeArtifactlari.AnyAsync(a => a.EBelgeKaydiId == eBelgeKaydiId && a.ArtifactAsamasi == EBelgeArtifactAsamasi.SignedReady));
    }

    [IntegrationFact]
    public async Task UnsignedMetadataImzalamaSirasindaDegistiktenSonraYeniClaimIleYenidenDenemeBasariliOlur()
    {
        await using var dbContext = CreateDbContext();
        var (eBelgeKaydiId, unsignedArtifact) = await SeedUnsignedArtifactAsync(dbContext);
        var claim = await SeedAndClaimUblImzalaOutboxAsync(dbContext, eBelgeKaydiId);

        if (_sidecarFixture.BaseUrl is null)
        {
            Assert.Fail($"Sidecar ayağa kaldırılamadı: {_sidecarFixture.AtlamaNedeni}");
        }

        var http = new HttpClient { BaseAddress = new Uri(_sidecarFixture.BaseUrl!), Timeout = TimeSpan.FromSeconds(15) };
        var mutator = new FieldMutatingSchematronDecorator(
            new SaxonSidecarEBelgeSchematronValidator(http),
            (ctx, ct) => ctx.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE [muhasebe].[EBelgeArtifactlari] SET [RuleSetId] = {"gecici-degisiklik"} WHERE [Id] = {unsignedArtifact.Id}", ct));

        var ilkService = new EBelgeUblImzalamaService(
            dbContext,
            new EBelgeXmlImzalayici(new EBelgeTestSertifikaSaglayici(), new EBelgeTestSertifikaGuvenPolicy()),
            new EBelgeXmlImzaDogrulayici(),
            new EBelgeUblXsdValidator(EBelgeUblRendererTestVerisi.KuralSetiYukle()),
            mutator,
            new EBelgeOutboxLeaseTransitionService(dbContext),
            EBelgeKurumPolitikaTestSupport.CreateAlwaysAktifServisi(dbContext),
            new EBelgeKurumPolitikaTransactionGuard(dbContext),
            EBelgeTestSigningActivationGate.Acik,
            TimeProvider.System,
            NullLogger<EBelgeUblImzalamaService>.Instance);

        var ilkSonuc = await ilkService.ImzalaAsync(TalepFromClaim(claim, _kurumId, eBelgeKaydiId));
        Assert.NotNull(ilkSonuc);
        Assert.Equal(EBelgeUblImzalamaSonucuTuru.GeciciHata, ilkSonuc!.SonucTuru);

        // Metadata değişikliği GERİ ALINIR (gerçek bir kalıcı bozulma DEĞİL, geçici bir yarış
        // durumunu temsil ediyordu) - AYNI outbox mesajı YENİ bir lease ile yeniden claim edilip
        // BAŞARIYLA tamamlanabilmelidir.
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [muhasebe].[EBelgeArtifactlari] SET [RuleSetId] = {unsignedArtifact.RuleSetId} WHERE [Id] = {unsignedArtifact.Id}");
        await BackdateLeaseExpiryAsync(dbContext, claim.OutboxMesajiId);

        var yeniClaim = await new EBelgeOutboxClaimLeaseService(dbContext, EBelgeTestSigningActivationGate.Acik).TryClaimNextAsync(TimeSpan.FromMinutes(5));
        Assert.NotNull(yeniClaim);

        var ikinciService = CreateService(dbContext);
        var ikinciSonuc = await ikinciService.ImzalaAsync(TalepFromClaim(yeniClaim!, _kurumId, eBelgeKaydiId));

        Assert.NotNull(ikinciSonuc);
        Assert.True(ikinciSonuc!.BasariliMi, $"{ikinciSonuc.SonucTuru}: {ikinciSonuc.HataKodu} {ikinciSonuc.HataMesaji}");

        await using var verifyCtx = CreateDbContext();
        Assert.True(await verifyCtx.EBelgeArtifactlari.AnyAsync(a => a.EBelgeKaydiId == eBelgeKaydiId && a.ArtifactAsamasi == EBelgeArtifactAsamasi.SignedReady));
        var kayit = await verifyCtx.EBelgeKayitlari.AsNoTracking().SingleAsync(x => x.Id == eBelgeKaydiId);
        Assert.Equal(EBelgeKaydiDurumu.SignedReady, kayit.Durum);
    }

    [IntegrationFact]
    public async Task MevcutSignedReadyImzaProfiliDegistirilirseIdempotentBasariOlmazMetadataUyumsuzKaliciHataUretir()
    {
        await using var dbContext = CreateDbContext();
        var (eBelgeKaydiId, unsignedArtifact) = await SeedUnsignedArtifactAsync(dbContext);
        var mevcutSigned = await SeedMatchingSignedReadyAsync(dbContext, eBelgeKaydiId, unsignedArtifact);

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [muhasebe].[EBelgeArtifactlari] SET [ImzaProfili] = {"DEGISTIRILMIS-PROFIL/9.9/9.9"} WHERE [Id] = {mevcutSigned.Id}");

        var claim = await SeedAndClaimUblImzalaOutboxAsync(dbContext, eBelgeKaydiId);
        var service = CreateService(dbContext);
        var sonuc = await service.ImzalaAsync(TalepFromClaim(claim, _kurumId, eBelgeKaydiId));

        Assert.NotNull(sonuc);
        Assert.Equal(EBelgeUblImzalamaSonucuTuru.AtomikKaliciHata, sonuc!.SonucTuru);
        Assert.Equal(EBelgeXmlImzaHataKodlari.SignedArtifactMetadataUyumsuz, sonuc.HataKodu);

        await using var verifyCtx = CreateDbContext();
        var kayit = await verifyCtx.EBelgeKayitlari.AsNoTracking().SingleAsync(x => x.Id == eBelgeKaydiId);
        Assert.Equal(EBelgeKaydiDurumu.ImzalamaKaliciHata, kayit.Durum);
    }

    [IntegrationFact]
    public async Task MevcutSignedReadyImzaAlgoritmasiDegistirilirseIdempotentBasariOlmazMetadataUyumsuzKaliciHataUretir()
    {
        await using var dbContext = CreateDbContext();
        var (eBelgeKaydiId, unsignedArtifact) = await SeedUnsignedArtifactAsync(dbContext);
        var mevcutSigned = await SeedMatchingSignedReadyAsync(dbContext, eBelgeKaydiId, unsignedArtifact);

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [muhasebe].[EBelgeArtifactlari] SET [ImzaAlgoritmasi] = {"http://example.org/degistirilmis-imza-algoritmasi"} WHERE [Id] = {mevcutSigned.Id}");

        var claim = await SeedAndClaimUblImzalaOutboxAsync(dbContext, eBelgeKaydiId);
        var service = CreateService(dbContext);
        var sonuc = await service.ImzalaAsync(TalepFromClaim(claim, _kurumId, eBelgeKaydiId));

        Assert.NotNull(sonuc);
        Assert.Equal(EBelgeUblImzalamaSonucuTuru.AtomikKaliciHata, sonuc!.SonucTuru);
        Assert.Equal(EBelgeXmlImzaHataKodlari.SignedArtifactMetadataUyumsuz, sonuc.HataKodu);

        await using var verifyCtx = CreateDbContext();
        var kayit = await verifyCtx.EBelgeKayitlari.AsNoTracking().SingleAsync(x => x.Id == eBelgeKaydiId);
        Assert.Equal(EBelgeKaydiDurumu.ImzalamaKaliciHata, kayit.Durum);
    }

    [IntegrationFact]
    public async Task MevcutSignedReadyDigestAlgoritmasiDegistirilirseIdempotentBasariOlmazMetadataUyumsuzKaliciHataUretir()
    {
        await using var dbContext = CreateDbContext();
        var (eBelgeKaydiId, unsignedArtifact) = await SeedUnsignedArtifactAsync(dbContext);
        var mevcutSigned = await SeedMatchingSignedReadyAsync(dbContext, eBelgeKaydiId, unsignedArtifact);

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [muhasebe].[EBelgeArtifactlari] SET [DigestAlgoritmasi] = {"http://example.org/degistirilmis-digest-algoritmasi"} WHERE [Id] = {mevcutSigned.Id}");

        var claim = await SeedAndClaimUblImzalaOutboxAsync(dbContext, eBelgeKaydiId);
        var service = CreateService(dbContext);
        var sonuc = await service.ImzalaAsync(TalepFromClaim(claim, _kurumId, eBelgeKaydiId));

        Assert.NotNull(sonuc);
        Assert.Equal(EBelgeUblImzalamaSonucuTuru.AtomikKaliciHata, sonuc!.SonucTuru);
        Assert.Equal(EBelgeXmlImzaHataKodlari.SignedArtifactMetadataUyumsuz, sonuc.HataKodu);

        await using var verifyCtx = CreateDbContext();
        var kayit = await verifyCtx.EBelgeKayitlari.AsNoTracking().SingleAsync(x => x.Id == eBelgeKaydiId);
        Assert.Equal(EBelgeKaydiDurumu.ImzalamaKaliciHata, kayit.Durum);
    }

    [IntegrationFact]
    public async Task MevcutSignedReadySertifikaParmakIziDegistirilirseIdempotentBasariOlmazMetadataUyumsuzKaliciHataUretir()
    {
        await using var dbContext = CreateDbContext();
        var (eBelgeKaydiId, unsignedArtifact) = await SeedUnsignedArtifactAsync(dbContext);
        var mevcutSigned = await SeedMatchingSignedReadyAsync(dbContext, eBelgeKaydiId, unsignedArtifact);

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [muhasebe].[EBelgeArtifactlari] SET [ImzalayanSertifikaSha256ParmakIzi] = {new string('d', 64)} WHERE [Id] = {mevcutSigned.Id}");

        var claim = await SeedAndClaimUblImzalaOutboxAsync(dbContext, eBelgeKaydiId);
        var service = CreateService(dbContext);
        var sonuc = await service.ImzalaAsync(TalepFromClaim(claim, _kurumId, eBelgeKaydiId));

        Assert.NotNull(sonuc);
        Assert.Equal(EBelgeUblImzalamaSonucuTuru.AtomikKaliciHata, sonuc!.SonucTuru);
        Assert.Equal(EBelgeXmlImzaHataKodlari.SignedArtifactMetadataUyumsuz, sonuc.HataKodu);

        await using var verifyCtx = CreateDbContext();
        var kayit = await verifyCtx.EBelgeKayitlari.AsNoTracking().SingleAsync(x => x.Id == eBelgeKaydiId);
        Assert.Equal(EBelgeKaydiDurumu.ImzalamaKaliciHata, kayit.Durum);
    }

    [IntegrationFact]
    public async Task MevcutSignedReadyImzalamaZamaniXmlSigningTimeIleEslesmezseIdempotentBasariOlmazMetadataUyumsuzKaliciHataUretir()
    {
        await using var dbContext = CreateDbContext();
        var (eBelgeKaydiId, unsignedArtifact) = await SeedUnsignedArtifactAsync(dbContext);
        var mevcutSigned = await SeedMatchingSignedReadyAsync(dbContext, eBelgeKaydiId, unsignedArtifact);

        // Saklanan sütun, XML'e GÖMÜLÜ xades:SigningTime'dan (imzalama anında SANİYE hassasiyetiyle
        // yazılmıştı) FARKLI bir güne kaydırılır - saniyeye kırpma sonrası BİLE asla eşleşmeyecek
        // kadar büyük bir fark (1 gün).
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [muhasebe].[EBelgeArtifactlari] SET [ImzalamaZamaniUtc] = {mevcutSigned.ImzalamaZamaniUtc!.Value.AddDays(1)} WHERE [Id] = {mevcutSigned.Id}");

        var claim = await SeedAndClaimUblImzalaOutboxAsync(dbContext, eBelgeKaydiId);
        var service = CreateService(dbContext);
        var sonuc = await service.ImzalaAsync(TalepFromClaim(claim, _kurumId, eBelgeKaydiId));

        Assert.NotNull(sonuc);
        Assert.Equal(EBelgeUblImzalamaSonucuTuru.AtomikKaliciHata, sonuc!.SonucTuru);
        Assert.Equal(EBelgeXmlImzaHataKodlari.SignedArtifactMetadataUyumsuz, sonuc.HataKodu);

        await using var verifyCtx = CreateDbContext();
        var kayit = await verifyCtx.EBelgeKayitlari.AsNoTracking().SingleAsync(x => x.Id == eBelgeKaydiId);
        Assert.Equal(EBelgeKaydiDurumu.ImzalamaKaliciHata, kayit.Durum);
    }

    [IntegrationFact]
    public async Task MevcutSignedReadyRuleSetIdDegistirilirseIdempotentBasariOlmazMetadataUyumsuzKaliciHataUretir()
    {
        await using var dbContext = CreateDbContext();
        var (eBelgeKaydiId, unsignedArtifact) = await SeedUnsignedArtifactAsync(dbContext);
        var mevcutSigned = await SeedMatchingSignedReadyAsync(dbContext, eBelgeKaydiId, unsignedArtifact);

        // SignedReady'nin kaynak zinciri alanlarından biri (RuleSetId) - kaynak Unsigned'la olan
        // bağdan BAĞIMSIZ olarak - değiştirilir (bkz. görev md.5, "RuleSetId veya snapshot zinciri
        // değiştirilirse idempotent başarı olmaz"; SnapshotSchemaVersion/KaynakSnapshotSha256 İÇİN
        // AYNI `kaynakZinciriEslesiyor` kontrolü yapısal olarak SİMETRİKTİR).
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [muhasebe].[EBelgeArtifactlari] SET [RuleSetId] = {"baska-bir-kural-seti"} WHERE [Id] = {mevcutSigned.Id}");

        var claim = await SeedAndClaimUblImzalaOutboxAsync(dbContext, eBelgeKaydiId);
        var service = CreateService(dbContext);
        var sonuc = await service.ImzalaAsync(TalepFromClaim(claim, _kurumId, eBelgeKaydiId));

        Assert.NotNull(sonuc);
        Assert.Equal(EBelgeUblImzalamaSonucuTuru.AtomikKaliciHata, sonuc!.SonucTuru);
        Assert.Equal(EBelgeXmlImzaHataKodlari.SignedArtifactMetadataUyumsuz, sonuc.HataKodu);

        await using var verifyCtx = CreateDbContext();
        var kayit = await verifyCtx.EBelgeKayitlari.AsNoTracking().SingleAsync(x => x.Id == eBelgeKaydiId);
        Assert.Equal(EBelgeKaydiDurumu.ImzalamaKaliciHata, kayit.Durum);
    }

    [IntegrationFact]
    public async Task MevcutSignedTxDisiDogrulamaSonrasiRuleSetIdDegistirilirseYarisDurumuDoner()
    {
        await using var dbContext = CreateDbContext();
        var (eBelgeKaydiId, unsignedArtifact) = await SeedUnsignedArtifactAsync(dbContext);
        var mevcutSigned = await SeedMatchingSignedReadyAsync(dbContext, eBelgeKaydiId, unsignedArtifact);
        var claim = await SeedAndClaimUblImzalaOutboxAsync(dbContext, eBelgeKaydiId);

        if (_sidecarFixture.BaseUrl is null)
        {
            Assert.Fail($"Sidecar ayağa kaldırılamadı: {_sidecarFixture.AtlamaNedeni}");
        }

        var http = new HttpClient { BaseAddress = new Uri(_sidecarFixture.BaseUrl!), Timeout = TimeSpan.FromSeconds(15) };
        var mutator = new FieldMutatingSchematronDecorator(
            new SaxonSidecarEBelgeSchematronValidator(http),
            (ctx, ct) => ctx.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE [muhasebe].[EBelgeArtifactlari] SET [RuleSetId] = {"degistirilmis-kural-seti-yaris"} WHERE [Id] = {mevcutSigned.Id}", ct));

        var service = new EBelgeUblImzalamaService(
            dbContext,
            new EBelgeXmlImzalayici(new EBelgeTestSertifikaSaglayici(), new EBelgeTestSertifikaGuvenPolicy()),
            new EBelgeXmlImzaDogrulayici(),
            new EBelgeUblXsdValidator(EBelgeUblRendererTestVerisi.KuralSetiYukle()),
            mutator,
            new EBelgeOutboxLeaseTransitionService(dbContext),
            EBelgeKurumPolitikaTestSupport.CreateAlwaysAktifServisi(dbContext),
            new EBelgeKurumPolitikaTransactionGuard(dbContext),
            EBelgeTestSigningActivationGate.Acik,
            TimeProvider.System,
            NullLogger<EBelgeUblImzalamaService>.Instance);

        var sonuc = await service.ImzalaAsync(TalepFromClaim(claim, _kurumId, eBelgeKaydiId));

        Assert.NotNull(sonuc);
        Assert.Equal(EBelgeUblImzalamaSonucuTuru.GeciciHata, sonuc!.SonucTuru);
        Assert.Equal(EBelgeXmlImzaHataKodlari.YarisDurumu, sonuc.HataKodu);

        await using var verifyCtx = CreateDbContext();
        var kayit = await verifyCtx.EBelgeKayitlari.AsNoTracking().SingleAsync(x => x.Id == eBelgeKaydiId);
        Assert.Equal(EBelgeKaydiDurumu.UnsignedUblHazir, kayit.Durum); // geçici hata DEĞİŞTİRMEZ
    }

    // ---- Faz 2B.10.1 görev md.8 - imza SONRASI (SignedReady yazılmadan ÖNCE) kurum politikası kill switch yarışı ----

    [IntegrationFact]
    [Trait("CriticalInvariant", "PolicyKillSwitchPreventsCommit")]
    public async Task ClaimSonrasiImzaSirasindaPolitikaPasifeAlinirsaSignedReadyYazilmaz()
    {
        await using var dbContext = CreateDbContext();
        var (eBelgeKaydiId, _) = await SeedUnsignedArtifactAsync(dbContext);
        var claim = await SeedAndClaimUblImzalaOutboxAsync(dbContext, eBelgeKaydiId);

        // Politika, claim SONRASINDA (signer bloklanır, politika pasife alınır, signer serbest
        // bırakılır yarışını simüle eder - imza operasyonu KENDİSİ tx dışıdır, bu yüzden burada
        // deterministik olarak, gerçek zamanda beklemeden, imzadan ÖNCE pasife alınır - pre-commit
        // kontrolü imza SONRASI/commit ÖNCESİ ÇALIŞTIĞINDAN sonuç AYNIDIR).
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [muhasebe].[KurumEBelgePolitikalari] SET [AktifMi] = 0 WHERE [KurumId] = {_kurumId}");

        var service = CreateService(dbContext);
        var sonuc = await service.ImzalaAsync(TalepFromClaim(claim, _kurumId, eBelgeKaydiId));

        Assert.NotNull(sonuc);
        Assert.Equal(EBelgeUblImzalamaSonucuTuru.AtomikPolitikaBloklu, sonuc!.SonucTuru);

        await using var verifyCtx = CreateDbContext();
        Assert.False(await verifyCtx.EBelgeArtifactlari.IgnoreQueryFilters().AnyAsync(a => a.EBelgeKaydiId == eBelgeKaydiId && a.ArtifactAsamasi == EBelgeArtifactAsamasi.SignedReady));

        var kayit = await verifyCtx.EBelgeKayitlari.AsNoTracking().SingleAsync(x => x.Id == eBelgeKaydiId);
        Assert.Equal(EBelgeKaydiDurumu.UnsignedUblHazir, kayit.Durum); // İLERLEMEDİ

        var outbox = await verifyCtx.EBelgeOutboxMesajlari.AsNoTracking().SingleAsync(x => x.Id == claim.OutboxMesajiId);
        Assert.Equal(EBelgeOutboxDurumu.Bekliyor, outbox.Durum); // terminalize EDİLMEDİ, teknik Hata DEĞİL
        Assert.Null(outbox.KilitToken);
        Assert.Equal(0, outbox.DenemeSayisi); // claim'de tüketilen deneme GERİ ALINDI

        await using var restoreCtx = CreateDbContext();
        await restoreCtx.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [muhasebe].[KurumEBelgePolitikalari] SET [AktifMi] = 1 WHERE [KurumId] = {_kurumId}");
    }

    /// <summary>
    /// Faz 2B.10.2 görev md.10 - yukarıdaki `ClaimSonrasiImzaSirasindaPolitikaPasifeAlinirsaSignedReadyYazilmaz`
    /// testi ("politika ÖNCE deaktive edilir, SONRA servis çağrılır") sıralı bir simülasyondur -
    /// GERÇEK TOCTOU penceresini KANITLAMAZ. Bu test GERÇEK, örtüşen iki transaction kurar: (1)
    /// "admin" bağlantısı politika satırını AÇIK bir transaction içinde AktifMi=0 yapar ama COMMIT
    /// ETMEZ, (2) worker'ın GERÇEK `ImzalaAsync` çağrısı AYRI bir Task olarak başlatılır - erken
    /// gate kontrolünü geçer (gate AÇIK), Faz1/Faz2'yi (imza/bağımsız doğrulama - tx DIŞI) GERÇEKTEN
    /// tamamlar, KISA commit transaction'ını açar ve politika satırını kilitlemeye çalışırken
    /// admin'in tuttuğu kilide ÇARPARAK GERÇEKTEN BLOKE OLUR - bu, worker task'ının makul bir süre
    /// içinde TAMAMLANMADIĞI doğrulanarak KANITLANIR. Admin COMMIT edildikten SONRA worker'ın GÜNCEL
    /// (pasif) politikayı GÖRDÜĞÜ ve SignedReady YAZMADIĞI doğrulanır.
    /// </summary>
    [IntegrationFact]
    [Trait("CriticalInvariant", "PolicyKillSwitchPreventsCommit")]
    public async Task GercekEszamanliKillSwitchImzaSirasindaWorkerBlokeEderVeSignedReadyYazdirmaz()
    {
        await using var dbContext = CreateDbContext();
        var (eBelgeKaydiId, _) = await SeedUnsignedArtifactAsync(dbContext);
        var claim = await SeedAndClaimUblImzalaOutboxAsync(dbContext, eBelgeKaydiId);

        await using var adminCtx = CreateDbContext();
        await using var adminTx = await adminCtx.Database.BeginTransactionAsync();
        await adminCtx.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [muhasebe].[KurumEBelgePolitikalari] SET [AktifMi] = 0 WHERE [KurumId] = {_kurumId}");

        await using var workerCtx = CreateDbContext();
        var service = CreateService(workerCtx);
        var workerTask = Task.Run(() => service.ImzalaAsync(TalepFromClaim(claim, _kurumId, eBelgeKaydiId)));

        var erkenTamamlandiMi = await Task.WhenAny(workerTask, Task.Delay(TimeSpan.FromSeconds(2))) == workerTask;
        Assert.False(erkenTamamlandiMi, "worker, admin'in tuttuğu politika satırı kilidine ÇARPMADI - test GERÇEK bir TOCTOU penceresi KURAMADI.");

        await adminTx.CommitAsync();

        var sonuc = await workerTask;

        Assert.NotNull(sonuc);
        Assert.Equal(EBelgeUblImzalamaSonucuTuru.AtomikPolitikaBloklu, sonuc!.SonucTuru);

        await using var verifyCtx = CreateDbContext();
        Assert.False(await verifyCtx.EBelgeArtifactlari.IgnoreQueryFilters().AnyAsync(a => a.EBelgeKaydiId == eBelgeKaydiId && a.ArtifactAsamasi == EBelgeArtifactAsamasi.SignedReady));

        var kayit = await verifyCtx.EBelgeKayitlari.AsNoTracking().SingleAsync(x => x.Id == eBelgeKaydiId);
        Assert.Equal(EBelgeKaydiDurumu.UnsignedUblHazir, kayit.Durum); // İLERLEMEDİ

        var outbox = await verifyCtx.EBelgeOutboxMesajlari.AsNoTracking().SingleAsync(x => x.Id == claim.OutboxMesajiId);
        Assert.Equal(EBelgeOutboxDurumu.Bekliyor, outbox.Durum);
        Assert.Equal(0, outbox.DenemeSayisi);

        await using var restoreCtx = CreateDbContext();
        await restoreCtx.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [muhasebe].[KurumEBelgePolitikalari] SET [AktifMi] = 1 WHERE [KurumId] = {_kurumId}");
    }

    // ---- Faz 2B.10.2 görev md.5-7/md.11 - global signing gate, GERÇEK commit-öncesi kapı ----

    [IntegrationFact]
    [Trait("CriticalInvariant", "SigningGatePreventsQueuedSigning")]
    public async Task SigningGateKapaliykenKuyruktakiMesajHicIslenmeyeBaslamazVeSignedReadyYazilmaz()
    {
        await using var dbContext = CreateDbContext();
        var (eBelgeKaydiId, unsignedArtifact) = await SeedUnsignedArtifactAsync(dbContext);
        var claim = await SeedAndClaimUblImzalaOutboxAsync(dbContext, eBelgeKaydiId);

        var service = CreateService(dbContext, signingActivationGate: EBelgeTestSigningActivationGate.Kapali);
        var sonuc = await service.ImzalaAsync(TalepFromClaim(claim, _kurumId, eBelgeKaydiId));

        Assert.NotNull(sonuc);
        Assert.Equal(EBelgeUblImzalamaSonucuTuru.AtomikPolitikaBloklu, sonuc!.SonucTuru);

        await using var verifyCtx = CreateDbContext();
        // Gate handler BAŞLAMADAN kapalı bulunduğundan, unsigned kaynak artefakt bile OKUNMAMIŞ
        // olmalıdır - erken kapı, imza/render işine HİÇ GİRMEDEN devreye girer.
        Assert.False(await verifyCtx.EBelgeArtifactlari.IgnoreQueryFilters().AnyAsync(a => a.EBelgeKaydiId == eBelgeKaydiId && a.ArtifactAsamasi == EBelgeArtifactAsamasi.SignedReady));

        var kayit = await verifyCtx.EBelgeKayitlari.AsNoTracking().SingleAsync(x => x.Id == eBelgeKaydiId);
        Assert.Equal(EBelgeKaydiDurumu.UnsignedUblHazir, kayit.Durum); // İLERLEMEDİ

        var outbox = await verifyCtx.EBelgeOutboxMesajlari.AsNoTracking().SingleAsync(x => x.Id == claim.OutboxMesajiId);
        Assert.Equal(EBelgeOutboxDurumu.Bekliyor, outbox.Durum); // terminalize EDİLMEDİ
        Assert.Null(outbox.KilitToken);
        Assert.Equal(0, outbox.DenemeSayisi); // claim'de tüketilen deneme GERİ ALINDI - retry churn YOK
    }

    [IntegrationFact]
    [Trait("CriticalInvariant", "SigningGatePreventsQueuedSigning")]
    public async Task SigningGateImzaSirasindaKapanirsaSignedReadyCommitEdilmezImzaSonucuDiscardEdilir()
    {
        await using var dbContext = CreateDbContext();
        var (eBelgeKaydiId, _) = await SeedUnsignedArtifactAsync(dbContext);
        var claim = await SeedAndClaimUblImzalaOutboxAsync(dbContext, eBelgeKaydiId);

        // Handler BAŞLARKEN gate AÇIK (erken kontrolü geçer) - imza/bağımsız doğrulama (tx dışı)
        // sırasında KAPANIR - commit-öncesi (ikinci) kontrol bunu YAKALAMALIDIR.
        var toggleGate = new ToggleSigningActivationGate(acikSayisi: 1);
        var service = CreateService(dbContext, signingActivationGate: toggleGate);
        var sonuc = await service.ImzalaAsync(TalepFromClaim(claim, _kurumId, eBelgeKaydiId));

        Assert.NotNull(sonuc);
        Assert.Equal(EBelgeUblImzalamaSonucuTuru.AtomikPolitikaBloklu, sonuc!.SonucTuru);
        Assert.True(toggleGate.CagriSayisi >= 2, "commit-öncesi (ikinci) gate kontrolü hiç ÇAĞRILMADI.");

        await using var verifyCtx = CreateDbContext();
        Assert.False(await verifyCtx.EBelgeArtifactlari.IgnoreQueryFilters().AnyAsync(a => a.EBelgeKaydiId == eBelgeKaydiId && a.ArtifactAsamasi == EBelgeArtifactAsamasi.SignedReady));

        var kayit = await verifyCtx.EBelgeKayitlari.AsNoTracking().SingleAsync(x => x.Id == eBelgeKaydiId);
        Assert.Equal(EBelgeKaydiDurumu.UnsignedUblHazir, kayit.Durum);

        var outbox = await verifyCtx.EBelgeOutboxMesajlari.AsNoTracking().SingleAsync(x => x.Id == claim.OutboxMesajiId);
        Assert.Equal(EBelgeOutboxDurumu.Bekliyor, outbox.Durum);
        Assert.Equal(0, outbox.DenemeSayisi);
    }

    [IntegrationFact]
    public async Task SigningGateTekrarAcilincaKuyruktakiMesajYenidenIslenebilirVeSignedReadyUretilir()
    {
        await using var dbContext = CreateDbContext();
        var (eBelgeKaydiId, _) = await SeedUnsignedArtifactAsync(dbContext);
        var claim = await SeedAndClaimUblImzalaOutboxAsync(dbContext, eBelgeKaydiId);

        var kapaliService = CreateService(dbContext, signingActivationGate: EBelgeTestSigningActivationGate.Kapali);
        var bloklandi = await kapaliService.ImzalaAsync(TalepFromClaim(claim, _kurumId, eBelgeKaydiId));
        Assert.Equal(EBelgeUblImzalamaSonucuTuru.AtomikPolitikaBloklu, bloklandi!.SonucTuru);

        // Gate tekrar açılır - claim filtresi mesajı ARTIK yeniden seçebilmelidir.
        var yenidenClaim = await new EBelgeOutboxClaimLeaseService(dbContext, EBelgeTestSigningActivationGate.Acik).TryClaimNextAsync(TimeSpan.FromSeconds(60));
        Assert.NotNull(yenidenClaim);
        Assert.Equal(claim.OutboxMesajiId, yenidenClaim!.OutboxMesajiId);

        var acikService = CreateService(dbContext, signingActivationGate: EBelgeTestSigningActivationGate.Acik);
        var sonuc = await acikService.ImzalaAsync(TalepFromClaim(yenidenClaim, _kurumId, eBelgeKaydiId));

        Assert.NotNull(sonuc);
        Assert.Equal(EBelgeUblImzalamaSonucuTuru.AtomikBasarili, sonuc!.SonucTuru);

        await using var verifyCtx = CreateDbContext();
        Assert.True(await verifyCtx.EBelgeArtifactlari.IgnoreQueryFilters().AnyAsync(a => a.EBelgeKaydiId == eBelgeKaydiId && a.ArtifactAsamasi == EBelgeArtifactAsamasi.SignedReady && !a.IsDeleted));
    }

    /// <summary>Faz 2B.10.2 görev md.11 - `CanSignNow()`'ı belirli sayıda çağrıdan SONRA kapatan, deterministik gate geçiş test double'ı (config hot-reload GEREKMEZ).</summary>
    private sealed class ToggleSigningActivationGate : IEBelgeSigningActivationGate
    {
        private readonly int _acikSayisi;
        private int _cagriSayisi;

        public ToggleSigningActivationGate(int acikSayisi) => _acikSayisi = acikSayisi;

        public int CagriSayisi => _cagriSayisi;

        public bool ShouldCreateSigningMessage() => true;

        public bool CanSignNow()
        {
            _cagriSayisi++;
            return _cagriSayisi <= _acikSayisi;
        }
    }

    [IntegrationFact]
    public async Task PolitikaBloklandigindaSonHataAlanlariImzaVeyaSertifikaBilgisiIcermez()
    {
        await using var dbContext = CreateDbContext();
        var (eBelgeKaydiId, _) = await SeedUnsignedArtifactAsync(dbContext);
        var claim = await SeedAndClaimUblImzalaOutboxAsync(dbContext, eBelgeKaydiId);

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [muhasebe].[KurumEBelgePolitikalari] SET [AktifMi] = 0 WHERE [KurumId] = {_kurumId}");

        var service = CreateService(dbContext);
        await service.ImzalaAsync(TalepFromClaim(claim, _kurumId, eBelgeKaydiId));

        await using var verifyCtx = CreateDbContext();
        var outbox = await verifyCtx.EBelgeOutboxMesajlari.AsNoTracking().SingleAsync(x => x.Id == claim.OutboxMesajiId);

        // SonHataMesaji dolu olsa bile (gözlemlenebilirlik amaçlı sabit bir işaret) - imza bytes'ı,
        // sertifika parmak izi veya XML içeriği ASLA içermemelidir.
        Assert.DoesNotContain("BEGIN CERTIFICATE", outbox.SonHataMesaji ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<", outbox.SonHataMesaji ?? string.Empty, StringComparison.Ordinal);

        await using var restoreCtx = CreateDbContext();
        await restoreCtx.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [muhasebe].[KurumEBelgePolitikalari] SET [AktifMi] = 1 WHERE [KurumId] = {_kurumId}");
    }
}
