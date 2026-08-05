using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using STYS.Infrastructure.EntityFramework;
using STYS.Kurumlar.Entities;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.SatisBelgeleri;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Muhasebe.SatisBelgeleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using STYS.Muhasebe.SatisBelgeleri.Services;
using STYS.Tesisler.Entities;
using TOD.Platform.SharedKernel.Exceptions;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// Faz 2B.6.1 - GERÇEK SQL Server'a karşı, GERÇEK renderer + GERÇEK Java Saxon sidecar ile
/// EBelgeArtefaktOlusturmaService'in outbox tüketim akışını doğrular (bkz. görev md.17,
/// "gerçek sidecar gereken en az bir outbox integration testi"). Snapshot içeriği, Faz 2B.5'te
/// zaten uçtan uca doğrulanmış EBelgeUblRendererTestVerisi.GecerliSnapshot()'tan türetilir -
/// snapshot ÜRETİMİ burada test EDİLMEZ (ayrı, önceki fazlarda test edildi); burada test edilen
/// KONU, VAROLAN bir immutable snapshot kaydından artefakt üretme/kalıcılaştırma akışıdır.
///
/// Faz 2B.6.1 ile artık HER OlusturAsync çağrısı GERÇEK, GEÇERLİ bir outbox lease talep eder
/// (bkz. EBelgeArtefaktOlusturmaTalebi.KilitToken/KilitBitisZamaniUtc) - bu yüzden testler önce
/// GERÇEK bir outbox mesajı seed edip GERÇEK claim SQL'i üzerinden (EBelgeOutboxClaimLeaseService)
/// bir lease alır, sonra o lease'in token'ıyla talep oluşturur. Lease süresinin dolması/başka bir
/// worker tarafından reclaim edilmesi gibi durumlar, DB tarafının SYSUTCDATETIME() kullanması
/// nedeniyle (bkz. EBelgeOutboxLeaseTransitionService - kasıtlı olarak değiştirilmedi) gerçek
/// zamanda beklemek yerine satırın KilitBitisZamaniUtc/KilitToken alanları DOĞRUDAN SQL ile
/// deterministik olarak manipüle edilerek simüle edilir (Task.Delay KULLANILMAZ).
/// </summary>
[Trait("Category", "Integration")]
[Collection(SqlServerIntegrationCollection.Name)]
public class EBelgeArtefaktOlusturmaServiceIntegrationTests : IAsyncLifetime, IClassFixture<SchematronSidecarProcessFixture>
{
    private const string TestMarker = "EBO-2B6";

    private readonly SchematronSidecarProcessFixture _sidecarFixture;
    private string _uniqueSuffix = TestMarker;
    private int _kurumId;
    private int _ilId;
    private int _tesisId;
    private int _musteriKartId;

    public EBelgeArtefaktOlusturmaServiceIntegrationTests(SchematronSidecarProcessFixture sidecarFixture)
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

        // EBelgeArtifactlari, EBelgeKayitlari'na Restrict FK ile bağlıdır (bilinçli tasarım -
        // bkz. görev md.16, "cascade delete kullanma") - genel temizlik yardımcısı bunu
        // BİLMEZ, bu yüzden artefaktlar ÖNCE burada, doğrudan silinir (soft-delete edilmiş
        // satırlar dahil - IgnoreQueryFilters olmadan DELETE zaten filtre uygulamaz).
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

    /// <summary>
    /// Faz 2B.5'te uçtan uca doğrulanmış GEÇERLİ bir V2 snapshot'ı, GERÇEK bir SatisBelgesi'ye
    /// bağlı YENİ bir EBelgeKaydi + EBelgeSnapshot olarak kalıcılaştırır (snapshot'ı CANLI
    /// entity'lerden YENİDEN ÜRETMEZ - zaten üretilmiş, sabit bir test snapshot'ını KAYDEDER).
    /// </summary>
    private async Task<int> SeedEBelgeKaydiWithV2SnapshotAsync(StysAppDbContext dbContext, EBelgeCanonicalSnapshotV2? snapshotOverride = null)
    {
        var satisBelgesiId = await CreateSatisBelgesiIdAsync(dbContext);

        await using var seedCtx = CreateDbContext();
        var v2Snapshot = snapshotOverride ?? EBelgeUblRendererTestVerisi.GecerliSnapshot();
        var utf8Bytes = JsonSerializer.SerializeToUtf8Bytes(v2Snapshot, EBelgeCanonicalSnapshotV2Reader.CanonicalJsonOptions);
        var hash = Convert.ToHexString(SHA256.HashData(utf8Bytes));
        var json = Encoding.UTF8.GetString(utf8Bytes);

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

        seedCtx.EBelgeSnapshots.Add(new EBelgeSnapshot
        {
            KurumId = _kurumId,
            EBelgeKaydiId = eBelgeKaydi.Id,
            BelgeVersiyonu = 1,
            SnapshotSchemaVersion = EBelgeCanonicalSnapshotV2Reader.SupportedSnapshotSchemaVersion,
            CanonicalJson = json,
            CanonicalSha256 = hash,
        });
        await seedCtx.SaveChangesAsync();

        return eBelgeKaydi.Id;
    }

    /// <summary>Gerçek bir Bekliyor outbox mesajı seed eder ve GERÇEK claim SQL'i (UPDLOCK/READPAST) üzerinden geçerli bir lease alır.</summary>
    private async Task<EBelgeOutboxClaimLeaseResultDto> SeedAndClaimOutboxAsync(StysAppDbContext dbContext, int eBelgeKaydiId, TimeSpan? leaseDuration = null)
    {
        dbContext.EBelgeOutboxMesajlari.Add(new EBelgeOutboxMesaji
        {
            KurumId = _kurumId,
            EBelgeKaydiId = eBelgeKaydiId,
            IsTuru = EBelgeOutboxIsTuru.ArtefaktOlustur,
            Durum = EBelgeOutboxDurumu.Bekliyor,
            DenemeSayisi = 0,
        });
        await dbContext.SaveChangesAsync();

        var claimService = new EBelgeOutboxClaimLeaseService(dbContext);
        var claim = await claimService.TryClaimNextAsync(leaseDuration ?? TimeSpan.FromMinutes(5));
        Assert.NotNull(claim);
        return claim!;
    }

    private static EBelgeArtefaktOlusturmaTalebi TalepFromClaim(EBelgeOutboxClaimLeaseResultDto claim, int kurumId, int eBelgeKaydiId)
        => new(kurumId, eBelgeKaydiId, claim.OutboxMesajiId, claim.KilitToken, claim.KilitBitisZamaniUtc);

    /// <summary>DB tarafındaki lease'i (SYSUTCDATETIME() tabanlı) deterministik biçimde GEÇMİŞE çeker - gerçek zamanda beklemek YERİNE.</summary>
    private static Task BackdateLeaseExpiryAsync(StysAppDbContext dbContext, int outboxMesajiId) =>
        dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [muhasebe].[EBelgeOutboxMesajlari] SET [KilitBitisZamaniUtc] = DATEADD(MINUTE, -1, SYSUTCDATETIME()) WHERE [Id] = {outboxMesajiId}");

    /// <summary>Satırın KilitToken'ını DEĞİŞTİREREK, mesajın (gerçekte olduğu gibi) BAŞKA bir worker tarafından reclaim edildiğini simüle eder.</summary>
    private static Task SimulateOwnershipLostAsync(StysAppDbContext dbContext, int outboxMesajiId)
    {
        var yeniToken = Guid.NewGuid().ToString("D");
        return dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [muhasebe].[EBelgeOutboxMesajlari] SET [KilitToken] = {yeniToken} WHERE [Id] = {outboxMesajiId}");
    }

    private EBelgeArtefaktOlusturmaService CreateService(StysAppDbContext dbContext, TimeProvider? timeProvider = null, IEBelgeSigningActivationGate? signingActivationGate = null)
    {
        if (_sidecarFixture.BaseUrl is null)
        {
            Assert.Fail($"Sidecar ayağa kaldırılamadı: {_sidecarFixture.AtlamaNedeni}");
        }

        return new EBelgeArtefaktOlusturmaService(
            dbContext,
            new EBelgeCanonicalSnapshotV2Reader(),
            RealRendererTestSupport.CreateRealRenderer(_sidecarFixture.BaseUrl!),
            new EBelgeOutboxLeaseTransitionService(dbContext),
            signingActivationGate ?? FakeSigningActivationGate.Kapali,
            timeProvider ?? TimeProvider.System,
            NullLogger<EBelgeArtefaktOlusturmaService>.Instance);
    }

    /// <summary>Gerçek EBelgeSigningActivationGate'in davranışını (Enabled/tarih kapısı) test etmeyen senaryolarda sabit bir sonuç döner - AYRI, açık bir test double'ı (bkz. Faz 2B.7 görev md.18 testleri için EBelgeSigningActivationGateTests).</summary>
    private sealed class FakeSigningActivationGate : IEBelgeSigningActivationGate
    {
        public static readonly FakeSigningActivationGate Kapali = new(false);
        public static readonly FakeSigningActivationGate Acik = new(true);

        private readonly bool _sonuc;
        private FakeSigningActivationGate(bool sonuc) => _sonuc = sonuc;
        public bool ShouldCreateSigningMessage() => _sonuc;
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _zaman;
        public FixedTimeProvider(DateTimeOffset zaman) => _zaman = zaman;
        public override DateTimeOffset GetUtcNow() => _zaman;
    }

    /// <summary>Gerçek renderer'ı sarar, yalnız beyan edilen hash'i BOZAR - runtime hash yeniden doğrulamasını (md.7) tetiklemek için.</summary>
    private sealed class HashBozanRendererDecorator : IEBelgeUblRenderer
    {
        private readonly IEBelgeUblRenderer _inner;
        public HashBozanRendererDecorator(IEBelgeUblRenderer inner) => _inner = inner;

        public async Task<EBelgeUblRenderSonucu> RenderAsync(EBelgeCanonicalSnapshotV2 snapshot, CancellationToken cancellationToken)
        {
            var sonuc = await _inner.RenderAsync(snapshot, cancellationToken);
            return sonuc with { UnsignedUblSha256 = new string('f', 64) };
        }
    }

    // ---- Talep doğrulaması (Faz 2B.6.2 görev md.5) ----

    [IntegrationFact]
    public async Task GecersizOutboxMesajiIdKurumIdVeyaEBelgeKaydiIdReddedilir()
    {
        await using var dbContext = CreateDbContext();
        var eBelgeKaydiId = await SeedEBelgeKaydiWithV2SnapshotAsync(dbContext);
        var claim = await SeedAndClaimOutboxAsync(dbContext, eBelgeKaydiId);
        var service = CreateService(dbContext);
        var gecerliToken = claim.KilitToken;

        await Assert.ThrowsAsync<BaseException>(() => service.OlusturAsync(
            new EBelgeArtefaktOlusturmaTalebi(_kurumId, eBelgeKaydiId, 0, gecerliToken, claim.KilitBitisZamaniUtc)));
        await Assert.ThrowsAsync<BaseException>(() => service.OlusturAsync(
            new EBelgeArtefaktOlusturmaTalebi(_kurumId, eBelgeKaydiId, -1, gecerliToken, claim.KilitBitisZamaniUtc)));
        await Assert.ThrowsAsync<BaseException>(() => service.OlusturAsync(
            new EBelgeArtefaktOlusturmaTalebi(0, eBelgeKaydiId, claim.OutboxMesajiId, gecerliToken, claim.KilitBitisZamaniUtc)));
        await Assert.ThrowsAsync<BaseException>(() => service.OlusturAsync(
            new EBelgeArtefaktOlusturmaTalebi(_kurumId, 0, claim.OutboxMesajiId, gecerliToken, claim.KilitBitisZamaniUtc)));
    }

    [IntegrationFact]
    public async Task GecersizFormatliKilitTokenReddedilir()
    {
        await using var dbContext = CreateDbContext();
        var eBelgeKaydiId = await SeedEBelgeKaydiWithV2SnapshotAsync(dbContext);
        var claim = await SeedAndClaimOutboxAsync(dbContext, eBelgeKaydiId);
        var service = CreateService(dbContext);

        await Assert.ThrowsAsync<BaseException>(() => service.OlusturAsync(
            new EBelgeArtefaktOlusturmaTalebi(_kurumId, eBelgeKaydiId, claim.OutboxMesajiId, "", claim.KilitBitisZamaniUtc)));
        await Assert.ThrowsAsync<BaseException>(() => service.OlusturAsync(
            new EBelgeArtefaktOlusturmaTalebi(_kurumId, eBelgeKaydiId, claim.OutboxMesajiId, "guid-degil", claim.KilitBitisZamaniUtc)));
    }

    // ---- Başarılı akış (senaryo 1, 15) ----

    [IntegrationFact]
    public async Task GecerliLeaseIleArtefaktEBelgeKaydiVeOutboxTekTransactionIleTamamlanir()
    {
        await using var dbContext = CreateDbContext();
        var eBelgeKaydiId = await SeedEBelgeKaydiWithV2SnapshotAsync(dbContext);
        var claim = await SeedAndClaimOutboxAsync(dbContext, eBelgeKaydiId);

        var sabitZaman = new DateTimeOffset(2026, 3, 4, 10, 0, 0, TimeSpan.Zero);
        var service = CreateService(dbContext, new FixedTimeProvider(sabitZaman));
        var sonuc = await service.OlusturAsync(TalepFromClaim(claim, _kurumId, eBelgeKaydiId));

        Assert.NotNull(sonuc);
        Assert.True(sonuc!.BasariliMi, $"{sonuc.SonucTuru}: {sonuc.HataKodu} {sonuc.HataMesaji}");
        Assert.Equal(EBelgeArtefaktOlusturmaSonucuTuru.AtomikBasarili, sonuc.SonucTuru);

        await using var verifyCtx = CreateDbContext();
        var artifact = await verifyCtx.EBelgeArtifactlari.AsNoTracking().SingleAsync(a => a.EBelgeKaydiId == eBelgeKaydiId);
        var kayit = await verifyCtx.EBelgeKayitlari.AsNoTracking().SingleAsync(x => x.Id == eBelgeKaydiId);
        var snapshot = await verifyCtx.EBelgeSnapshots.AsNoTracking().SingleAsync(x => x.EBelgeKaydiId == eBelgeKaydiId);
        var outbox = await verifyCtx.EBelgeOutboxMesajlari.AsNoTracking().SingleAsync(x => x.Id == claim.OutboxMesajiId);

        Assert.Equal(EBelgeKaydiDurumu.UnsignedUblHazir, kayit.Durum);
        Assert.Equal(snapshot.CanonicalSha256, artifact.KaynakSnapshotSha256);
        // İçerik EXACT byte'lar üzerinden saklanır - yeniden serialize edilmemiştir (senaryo 15).
        Assert.Equal(Convert.ToHexString(SHA256.HashData(artifact.Icerik)), artifact.ArtifactSha256);
        Assert.Equal("application/xml", artifact.MimeType);
        Assert.EndsWith(".xml", artifact.DosyaAdi, StringComparison.Ordinal);
        Assert.DoesNotContain("/", artifact.DosyaAdi, StringComparison.Ordinal);
        Assert.DoesNotContain("\\", artifact.DosyaAdi, StringComparison.Ordinal);
        Assert.Equal(sabitZaman.UtcDateTime, artifact.OlusturulmaZamaniUtc);

        // Outbox, artefakt+EBelgeKaydi ile AYNI atomik transaction'da Tamamlandi'ya geçmiştir
        // (senaryo 1) - lease alanları temizlenmiştir.
        Assert.Equal(EBelgeOutboxDurumu.Tamamlandi, outbox.Durum);
        Assert.Null(outbox.KilitToken);
        Assert.Null(outbox.KilitBitisZamaniUtc);
    }

    [IntegrationFact]
    public async Task OnceOnceSeedliHashEslesenMevcutArtefaktIdempotentBasariylaTamamlanirIkinciSatirEklenmez()
    {
        // [muhasebe].[EBelgeOutboxMesajlari] üzerinde (EBelgeKaydiId, IsTuru) BENZERSİZ indeksi
        // olduğundan, aynı e-belge kaydı için İKİNCİ bir outbox mesajı seed EDİLEMEZ - bu yüzden
        // idempotent-eşleşme (senaryo 12'nin "hash zinciri eşleşiyorsa" dalı) burada, render
        // SONUCUYLA TAM eşleşen bir artefaktı ÖNCEDEN (ör. bir veri taşıma/backfill senaryosunu
        // temsilen) seed ederek doğrulanır - renderer DETERMİNİSTİK olduğundan (bkz. Faz 2B.5)
        // gerçek bir render çağrısıyla üretilen hash, servis içindeki render ile AYNI olacaktır.
        await using var dbContext = CreateDbContext();
        var eBelgeKaydiId = await SeedEBelgeKaydiWithV2SnapshotAsync(dbContext);

        if (_sidecarFixture.BaseUrl is null)
        {
            Assert.Fail($"Sidecar ayağa kaldırılamadı: {_sidecarFixture.AtlamaNedeni}");
        }

        var v2Snapshot = EBelgeUblRendererTestVerisi.GecerliSnapshot();
        var beklenenRenderSonucu = await RealRendererTestSupport.CreateRealRenderer(_sidecarFixture.BaseUrl!)
            .RenderAsync(v2Snapshot, CancellationToken.None);
        var snapshotEntity = await dbContext.EBelgeSnapshots.AsNoTracking().SingleAsync(x => x.EBelgeKaydiId == eBelgeKaydiId);
        var xmlBytes = beklenenRenderSonucu.UnsignedUblUtf8.ToArray();

        dbContext.EBelgeArtifactlari.Add(new EBelgeArtifact
        {
            KurumId = _kurumId,
            EBelgeKaydiId = eBelgeKaydiId,
            ArtifactTipi = EBelgeArtifactTipi.UblXml,
            ArtifactAsamasi = EBelgeArtifactAsamasi.Unsigned,
            RuleSetId = beklenenRenderSonucu.KuralSetiKimligi,
            SnapshotSchemaVersion = int.Parse(EBelgeCanonicalSnapshotV2Reader.SupportedSnapshotSchemaVersion),
            KaynakSnapshotSha256 = snapshotEntity.CanonicalSha256,
            ArtifactSha256 = beklenenRenderSonucu.UnsignedUblSha256,
            Icerik = xmlBytes,
            MimeType = "application/xml",
            DosyaAdi = "onceden-seedli.xml",
            OlusturulmaZamaniUtc = DateTime.UtcNow,
        });
        await dbContext.SaveChangesAsync();

        var claim = await SeedAndClaimOutboxAsync(dbContext, eBelgeKaydiId);
        var service = CreateService(dbContext);
        var sonuc = await service.OlusturAsync(TalepFromClaim(claim, _kurumId, eBelgeKaydiId));

        Assert.Equal(EBelgeArtefaktOlusturmaSonucuTuru.AtomikBasarili, sonuc!.SonucTuru);

        await using var verifyCtx = CreateDbContext();
        var sayi = await verifyCtx.EBelgeArtifactlari.CountAsync(a => a.EBelgeKaydiId == eBelgeKaydiId);
        Assert.Equal(1, sayi); // ikinci (kendi) satır EKLENMEDİ - mevcut satır aynen kaldı
        var artifact = await verifyCtx.EBelgeArtifactlari.AsNoTracking().SingleAsync(a => a.EBelgeKaydiId == eBelgeKaydiId);
        Assert.Equal("onceden-seedli.xml", artifact.DosyaAdi); // ORİJİNAL satır - üzerine YAZILMADI
        var outbox = await verifyCtx.EBelgeOutboxMesajlari.AsNoTracking().SingleAsync(x => x.Id == claim.OutboxMesajiId);
        Assert.Equal(EBelgeOutboxDurumu.Tamamlandi, outbox.Durum);
        var kayit = await verifyCtx.EBelgeKayitlari.AsNoTracking().SingleAsync(x => x.Id == eBelgeKaydiId);
        Assert.Equal(EBelgeKaydiDurumu.UnsignedUblHazir, kayit.Durum);
    }

    [IntegrationFact]
    public async Task AyniLeaseIleEszamanliIkiYazmaDenemesindeYalnizBiriBasariliOlurArtefaktCoklanmaz()
    {
        await using var seedCtx = CreateDbContext();
        var eBelgeKaydiId = await SeedEBelgeKaydiWithV2SnapshotAsync(seedCtx);
        var claim = await SeedAndClaimOutboxAsync(seedCtx, eBelgeKaydiId);

        await using var ctx1 = CreateDbContext();
        await using var ctx2 = CreateDbContext();
        var service1 = CreateService(ctx1);
        var service2 = CreateService(ctx2);

        // Aynı GEÇERLİ lease token'ıyla eşzamanlı iki yazma denemesi - IsOwnedAsync'in UPDLOCK'u
        // satırı transaction commit/rollback olana kadar KİLİTLER (bkz. görev md.2), bu yüzden
        // KAZANAN transaction commit olup satırı Tamamlandi/KilitToken=NULL yaptıktan SONRA
        // açılan ikinci ownership kontrolü artık Durum=Isleniyor GÖRMEZ ve GÜVENLE
        // SahiplikKaybedildi döner - iki kez artefakt YAZILMAZ, exception FIRLATILMAZ.
        var talep = TalepFromClaim(claim, _kurumId, eBelgeKaydiId);
        var sonuclar = await Task.WhenAll(
            service1.OlusturAsync(talep),
            service2.OlusturAsync(talep));

        Assert.Single(sonuclar, s => s!.SonucTuru == EBelgeArtefaktOlusturmaSonucuTuru.AtomikBasarili);
        Assert.Single(sonuclar, s => s!.SonucTuru == EBelgeArtefaktOlusturmaSonucuTuru.SahiplikKaybedildi);

        await using var verifyCtx = CreateDbContext();
        var sayi = await verifyCtx.EBelgeArtifactlari.CountAsync(a => a.EBelgeKaydiId == eBelgeKaydiId);
        Assert.Equal(1, sayi);
    }

    // ---- Çapraz kayıt bağlama (Faz 2B.6.2 görev md.1-2, md.6 senaryo 1-4) ----

    [IntegrationFact]
    public async Task OutboxAninTokenIYanlisEBelgeKaydiIleKullanilamazHicbirKayitDegismezOutboxTerminalizeEdilmez()
    {
        await using var dbContext = CreateDbContext();
        // İKİ farklı, GERÇEK EBelgeKaydi - AYNI kurumda. Outbox A yalnız kaydiA'ya bağlı olarak
        // claim edilir; talep KASITLI OLARAK kaydiB'yi hedefler (aynı kurum, doğru/geçerli token,
        // yanlış EBelgeKaydiId) - bkz. görev md.1-2, senaryo 1-4.
        var kaydiAId = await SeedEBelgeKaydiWithV2SnapshotAsync(dbContext);
        var kaydiBId = await SeedEBelgeKaydiWithV2SnapshotAsync(dbContext);
        var claimA = await SeedAndClaimOutboxAsync(dbContext, kaydiAId);

        var service = CreateService(dbContext);
        var yanlisTalep = new EBelgeArtefaktOlusturmaTalebi(_kurumId, kaydiBId, claimA.OutboxMesajiId, claimA.KilitToken, claimA.KilitBitisZamaniUtc);
        var sonuc = await service.OlusturAsync(yanlisTalep);

        Assert.NotNull(sonuc);
        Assert.Equal(EBelgeArtefaktOlusturmaSonucuTuru.SahiplikKaybedildi, sonuc!.SonucTuru);

        await using var verifyCtx = CreateDbContext();
        // Ne kaydiA'ya (outbox'ın GERÇEK hedefi) ne kaydiB'ye (talebin YANLIŞ hedefi) artefakt oluşmadı.
        Assert.False(await verifyCtx.EBelgeArtifactlari.AnyAsync(a => a.EBelgeKaydiId == kaydiAId));
        Assert.False(await verifyCtx.EBelgeArtifactlari.AnyAsync(a => a.EBelgeKaydiId == kaydiBId));

        var kaydiA = await verifyCtx.EBelgeKayitlari.AsNoTracking().SingleAsync(x => x.Id == kaydiAId);
        var kaydiB = await verifyCtx.EBelgeKayitlari.AsNoTracking().SingleAsync(x => x.Id == kaydiBId);
        Assert.Equal(EBelgeKaydiDurumu.SnapshotHazir, kaydiA.Durum);
        Assert.Equal(EBelgeKaydiDurumu.SnapshotHazir, kaydiB.Durum);

        // Outbox A hâlâ Isleniyor - YANLIŞ hedefli bir talep yüzünden terminalize EDİLMEDİ.
        var outboxA = await verifyCtx.EBelgeOutboxMesajlari.AsNoTracking().SingleAsync(x => x.Id == claimA.OutboxMesajiId);
        Assert.Equal(EBelgeOutboxDurumu.Isleniyor, outboxA.Durum);
        Assert.NotNull(outboxA.KilitToken);
    }

    // ---- Lease sahipliği (senaryo 3, 4, 5, 6, 7) ----

    [IntegrationFact]
    public async Task LeaseSuresiRenderSirasindaDolmussaArtefaktOlusturulmazVeKayitDegismez()
    {
        await using var dbContext = CreateDbContext();
        var eBelgeKaydiId = await SeedEBelgeKaydiWithV2SnapshotAsync(dbContext);
        var claim = await SeedAndClaimOutboxAsync(dbContext, eBelgeKaydiId);

        // Render'ın DB dışı, uzun sürebilen bölümü sırasında lease'in dolduğunu simüle eder.
        await BackdateLeaseExpiryAsync(dbContext, claim.OutboxMesajiId);

        var service = CreateService(dbContext);
        var sonuc = await service.OlusturAsync(TalepFromClaim(claim, _kurumId, eBelgeKaydiId));

        Assert.Equal(EBelgeArtefaktOlusturmaSonucuTuru.SahiplikKaybedildi, sonuc!.SonucTuru);

        await using var verifyCtx = CreateDbContext();
        Assert.False(await verifyCtx.EBelgeArtifactlari.AnyAsync(a => a.EBelgeKaydiId == eBelgeKaydiId));
        var kayit = await verifyCtx.EBelgeKayitlari.AsNoTracking().SingleAsync(x => x.Id == eBelgeKaydiId);
        Assert.Equal(EBelgeKaydiDurumu.SnapshotHazir, kayit.Durum);
        var outbox = await verifyCtx.EBelgeOutboxMesajlari.AsNoTracking().SingleAsync(x => x.Id == claim.OutboxMesajiId);
        Assert.Equal(EBelgeOutboxDurumu.Isleniyor, outbox.Durum);
    }

    [IntegrationFact]
    public async Task ReclaimEdilmisMesajdaEskiWorkerYazamazSadeceYeniSahipYazar()
    {
        await using var seedCtx = CreateDbContext();
        var eBelgeKaydiId = await SeedEBelgeKaydiWithV2SnapshotAsync(seedCtx);
        var eskiClaim = await SeedAndClaimOutboxAsync(seedCtx, eBelgeKaydiId);

        // Eski worker'ın lease'i dolar, mesaj İKİNCİ bir worker tarafından GERÇEKTEN reclaim
        // edilir (yeni, farklı bir KilitToken alır) - senaryo 4/5/6/7.
        await BackdateLeaseExpiryAsync(seedCtx, eskiClaim.OutboxMesajiId);

        await using var reclaimCtx = CreateDbContext();
        var yeniClaim = await new EBelgeOutboxClaimLeaseService(reclaimCtx).TryClaimNextAsync(TimeSpan.FromMinutes(5));
        Assert.NotNull(yeniClaim);
        Assert.Equal(eskiClaim.OutboxMesajiId, yeniClaim!.OutboxMesajiId);
        Assert.NotEqual(eskiClaim.KilitToken, yeniClaim.KilitToken);

        await using var eskiWorkerCtx = CreateDbContext();
        await using var yeniWorkerCtx = CreateDbContext();
        var eskiSonuc = await CreateService(eskiWorkerCtx).OlusturAsync(TalepFromClaim(eskiClaim, _kurumId, eBelgeKaydiId));
        var yeniSonuc = await CreateService(yeniWorkerCtx).OlusturAsync(TalepFromClaim(yeniClaim, _kurumId, eBelgeKaydiId));

        Assert.Equal(EBelgeArtefaktOlusturmaSonucuTuru.SahiplikKaybedildi, eskiSonuc!.SonucTuru);
        Assert.Equal(EBelgeArtefaktOlusturmaSonucuTuru.AtomikBasarili, yeniSonuc!.SonucTuru);

        await using var verifyCtx = CreateDbContext();
        var sayi = await verifyCtx.EBelgeArtifactlari.CountAsync(a => a.EBelgeKaydiId == eBelgeKaydiId);
        Assert.Equal(1, sayi);
        var kayit = await verifyCtx.EBelgeKayitlari.AsNoTracking().SingleAsync(x => x.Id == eBelgeKaydiId);
        Assert.Equal(EBelgeKaydiDurumu.UnsignedUblHazir, kayit.Durum);
    }

    [IntegrationFact]
    public async Task KaliciHataYolundaSahiplikKaybedilmisseHicbirSeyDegismez()
    {
        // Kalıcı hataya (desteklenmeyen snapshot şema sürümü) düşecek bir kayıt seed edilir.
        await using var dbContext = CreateDbContext();
        var satisBelgesiId = await CreateSatisBelgesiIdAsync(dbContext);

        var eBelgeKaydi = new EBelgeKaydi
        {
            KurumId = _kurumId,
            SatisBelgesiId = satisBelgesiId,
            EBelgeUuid = Guid.NewGuid().ToString("D"),
            EBelgeKanali = EBelgeKanali.EArsiv,
            Durum = EBelgeKaydiDurumu.SnapshotHazir,
        };
        dbContext.EBelgeKayitlari.Add(eBelgeKaydi);
        await dbContext.SaveChangesAsync();

        const string v1BenzeriJson = "{\"surum\":\"1\"}";
        var v1Bytes = Encoding.UTF8.GetBytes(v1BenzeriJson);
        dbContext.EBelgeSnapshots.Add(new EBelgeSnapshot
        {
            KurumId = _kurumId,
            EBelgeKaydiId = eBelgeKaydi.Id,
            BelgeVersiyonu = 1,
            SnapshotSchemaVersion = "1",
            CanonicalJson = v1BenzeriJson,
            CanonicalSha256 = Convert.ToHexString(SHA256.HashData(v1Bytes)),
        });
        await dbContext.SaveChangesAsync();

        var claim = await SeedAndClaimOutboxAsync(dbContext, eBelgeKaydi.Id);

        // Sahiplik, render ile atomik hata transaction'ı arasında (başka bir worker reclaim
        // etmiş gibi) kaybedilir.
        await SimulateOwnershipLostAsync(dbContext, claim.OutboxMesajiId);

        var service = CreateService(dbContext);
        var sonuc = await service.OlusturAsync(TalepFromClaim(claim, _kurumId, eBelgeKaydi.Id));

        Assert.Equal(EBelgeArtefaktOlusturmaSonucuTuru.SahiplikKaybedildi, sonuc!.SonucTuru);

        await using var verifyCtx = CreateDbContext();
        var kayit = await verifyCtx.EBelgeKayitlari.AsNoTracking().SingleAsync(x => x.Id == eBelgeKaydi.Id);
        Assert.Equal(EBelgeKaydiDurumu.SnapshotHazir, kayit.Durum); // KaliciHata'ya GEÇMEDİ
        var outbox = await verifyCtx.EBelgeOutboxMesajlari.AsNoTracking().SingleAsync(x => x.Id == claim.OutboxMesajiId);
        Assert.Equal(EBelgeOutboxDurumu.Isleniyor, outbox.Durum); // hâlâ Isleniyor - Hata'ya geçmedi
    }

    // ---- Kalıcı hatalar (senaryo 9) ----

    [IntegrationFact]
    public async Task DesteklenmeyenSnapshotSemaSurumuAtomikKaliciHataOlurArtefaktOlusmaz()
    {
        await using var dbContext = CreateDbContext();
        var satisBelgesiId = await CreateSatisBelgesiIdAsync(dbContext);

        var eBelgeKaydi = new EBelgeKaydi
        {
            KurumId = _kurumId,
            SatisBelgesiId = satisBelgesiId,
            EBelgeUuid = Guid.NewGuid().ToString("D"),
            EBelgeKanali = EBelgeKanali.EArsiv,
            Durum = EBelgeKaydiDurumu.SnapshotHazir,
        };
        dbContext.EBelgeKayitlari.Add(eBelgeKaydi);
        await dbContext.SaveChangesAsync();

        // V1 benzeri, geçersiz/desteklenmeyen bir "canonical" gövde - şema sürümü "2" değil.
        const string v1BenzeriJson = "{\"surum\":\"1\"}";
        var v1Bytes = Encoding.UTF8.GetBytes(v1BenzeriJson);
        dbContext.EBelgeSnapshots.Add(new EBelgeSnapshot
        {
            KurumId = _kurumId,
            EBelgeKaydiId = eBelgeKaydi.Id,
            BelgeVersiyonu = 1,
            SnapshotSchemaVersion = "1",
            CanonicalJson = v1BenzeriJson,
            CanonicalSha256 = Convert.ToHexString(SHA256.HashData(v1Bytes)),
        });
        await dbContext.SaveChangesAsync();

        var claim = await SeedAndClaimOutboxAsync(dbContext, eBelgeKaydi.Id);
        var service = CreateService(dbContext);
        var sonuc = await service.OlusturAsync(TalepFromClaim(claim, _kurumId, eBelgeKaydi.Id));

        Assert.NotNull(sonuc);
        Assert.Equal(EBelgeArtefaktOlusturmaSonucuTuru.AtomikKaliciHata, sonuc!.SonucTuru);

        await using var verifyCtx = CreateDbContext();
        Assert.False(await verifyCtx.EBelgeArtifactlari.AnyAsync(a => a.EBelgeKaydiId == eBelgeKaydi.Id));
        var kayit = await verifyCtx.EBelgeKayitlari.AsNoTracking().SingleAsync(x => x.Id == eBelgeKaydi.Id);
        Assert.Equal(EBelgeKaydiDurumu.UnsignedUblKaliciHata, kayit.Durum);

        // Outbox, EBelgeKaydi ile AYNI atomik transaction'da terminal Hata'ya geçmiştir - retry PLANLANMAMIŞTIR.
        var outbox = await verifyCtx.EBelgeOutboxMesajlari.AsNoTracking().SingleAsync(x => x.Id == claim.OutboxMesajiId);
        Assert.Equal(EBelgeOutboxDurumu.Hata, outbox.Durum);
        Assert.Null(outbox.SonrakiDenemeZamaniUtc);
        Assert.Null(outbox.KilitToken);
    }

    [IntegrationFact]
    public async Task SnapshotHashUyusmazligiAtomikKaliciHataOlur()
    {
        await using var dbContext = CreateDbContext();
        var satisBelgesiId = await CreateSatisBelgesiIdAsync(dbContext);

        var eBelgeKaydi = new EBelgeKaydi
        {
            KurumId = _kurumId,
            SatisBelgesiId = satisBelgesiId,
            EBelgeUuid = Guid.NewGuid().ToString("D"),
            EBelgeKanali = EBelgeKanali.EArsiv,
            Durum = EBelgeKaydiDurumu.SnapshotHazir,
        };
        dbContext.EBelgeKayitlari.Add(eBelgeKaydi);
        await dbContext.SaveChangesAsync();

        var v2Snapshot = EBelgeUblRendererTestVerisi.GecerliSnapshot();
        var utf8Bytes = JsonSerializer.SerializeToUtf8Bytes(v2Snapshot, EBelgeCanonicalSnapshotV2Reader.CanonicalJsonOptions);
        var json = Encoding.UTF8.GetString(utf8Bytes);

        dbContext.EBelgeSnapshots.Add(new EBelgeSnapshot
        {
            KurumId = _kurumId,
            EBelgeKaydiId = eBelgeKaydi.Id,
            BelgeVersiyonu = 1,
            SnapshotSchemaVersion = EBelgeCanonicalSnapshotV2Reader.SupportedSnapshotSchemaVersion,
            CanonicalJson = json,
            CanonicalSha256 = new string('0', 64), // kasıtlı olarak YANLIŞ hash
        });
        await dbContext.SaveChangesAsync();

        var claim = await SeedAndClaimOutboxAsync(dbContext, eBelgeKaydi.Id);
        var service = CreateService(dbContext);
        var sonuc = await service.OlusturAsync(TalepFromClaim(claim, _kurumId, eBelgeKaydi.Id));

        Assert.NotNull(sonuc);
        Assert.Equal(EBelgeArtefaktOlusturmaSonucuTuru.AtomikKaliciHata, sonuc!.SonucTuru);
        Assert.Equal(EBelgeCanonicalSnapshotException.SafeErrorCode, sonuc.HataKodu);
    }

    [IntegrationFact]
    public async Task EBelgeKaydiBulunamazsaAtomikKaliciHataOlur()
    {
        // Faz 2B.6.2 öncesi bu test, GERÇEK bir claim'in EBelgeKaydiId'sini KASITLI OLARAK var
        // olmayan bir talep.EBelgeKaydiId ile eşleştirerek "bulunamadı" durumunu simüle ediyordu -
        // ama artık ownership kontrolü EBelgeKaydiId'yi de doğruladığından (bkz. görev md.1), bu
        // eşleşmeyen kombinasyon artık kayıt-arama aşamasına HİÇ ULAŞMADAN SahiplikKaybedildi ile
        // reddediliyor (bkz. OutboxAninTokenIYanlisEBelgeKaydiIleKullanilamaz... testi). Ayrıca FK
        // kısıtı (`FK_EBelgeOutboxMesajlari_EBelgeKayitlari_EBelgeKaydiId_KurumId`) bir outbox
        // satırının hiç var olmayan bir EBelgeKaydiId'ye işaret etmesini zaten YAPISAL olarak
        // engeller. Bu yüzden "kayıt bulunamadı" artık yalnız GERÇEKÇİ biçimde, doğru
        // EBelgeKaydiId'li bir talep ile ama kaydın SOFT-DELETE edilmiş olmasıyla (global EF
        // sorgu filtresi Faz 1 okumasını GÖRMEZ) üretilir.
        await using var dbContext = CreateDbContext();
        var eBelgeKaydiId = await SeedEBelgeKaydiWithV2SnapshotAsync(dbContext);
        var claim = await SeedAndClaimOutboxAsync(dbContext, eBelgeKaydiId);
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [muhasebe].[EBelgeKayitlari] SET [IsDeleted] = 1 WHERE [Id] = {eBelgeKaydiId}");

        var service = CreateService(dbContext);
        var sonuc = await service.OlusturAsync(TalepFromClaim(claim, _kurumId, eBelgeKaydiId));

        Assert.NotNull(sonuc);
        Assert.Equal(EBelgeArtefaktOlusturmaSonucuTuru.AtomikKaliciHata, sonuc!.SonucTuru);
        Assert.Equal("EBELGE_KAYDI_BULUNAMADI", sonuc.HataKodu);

        await using var verifyCtx = CreateDbContext();
        var outbox = await verifyCtx.EBelgeOutboxMesajlari.AsNoTracking().SingleAsync(x => x.Id == claim.OutboxMesajiId);
        Assert.Equal(EBelgeOutboxDurumu.Hata, outbox.Durum);
        Assert.Null(outbox.SonrakiDenemeZamaniUtc);
    }

    [IntegrationFact]
    public async Task YanlisKurumIdIleTalepSahiplikKaybedildiDonerVeHicbirSeyDegismez()
    {
        await using var dbContext = CreateDbContext();
        var eBelgeKaydiId = await SeedEBelgeKaydiWithV2SnapshotAsync(dbContext);
        var claim = await SeedAndClaimOutboxAsync(dbContext, eBelgeKaydiId);

        var service = CreateService(dbContext);
        // Yanlış KurumId, gerçek claim'in satırındaki KurumId ile eşleşmez - ownership katmanı
        // (multi-tenant izolasyonu dahil) bunu "kayıt bulunamadı" aşamasına gelmeden REDDEDER.
        var talep = new EBelgeArtefaktOlusturmaTalebi(_kurumId + 999_000, eBelgeKaydiId, claim.OutboxMesajiId, claim.KilitToken, claim.KilitBitisZamaniUtc);
        var sonuc = await service.OlusturAsync(talep);

        Assert.NotNull(sonuc);
        Assert.Equal(EBelgeArtefaktOlusturmaSonucuTuru.SahiplikKaybedildi, sonuc!.SonucTuru);

        await using var verifyCtx = CreateDbContext();
        Assert.False(await verifyCtx.EBelgeArtifactlari.AnyAsync(a => a.EBelgeKaydiId == eBelgeKaydiId));
        var outbox = await verifyCtx.EBelgeOutboxMesajlari.AsNoTracking().SingleAsync(x => x.Id == claim.OutboxMesajiId);
        Assert.Equal(EBelgeOutboxDurumu.Isleniyor, outbox.Durum);
    }

    // ---- Runtime hash doğrulaması (senaryo 14) ----

    [IntegrationFact]
    public async Task RuntimeHashUyusmazligiAtomikKaliciHataUretirArtefaktOlusmaz()
    {
        await using var dbContext = CreateDbContext();
        var eBelgeKaydiId = await SeedEBelgeKaydiWithV2SnapshotAsync(dbContext);
        var claim = await SeedAndClaimOutboxAsync(dbContext, eBelgeKaydiId);

        if (_sidecarFixture.BaseUrl is null)
        {
            Assert.Fail($"Sidecar ayağa kaldırılamadı: {_sidecarFixture.AtlamaNedeni}");
        }

        var gercekRenderer = RealRendererTestSupport.CreateRealRenderer(_sidecarFixture.BaseUrl!);
        var service = new EBelgeArtefaktOlusturmaService(
            dbContext,
            new EBelgeCanonicalSnapshotV2Reader(),
            new HashBozanRendererDecorator(gercekRenderer),
            new EBelgeOutboxLeaseTransitionService(dbContext),
            FakeSigningActivationGate.Kapali,
            TimeProvider.System,
            NullLogger<EBelgeArtefaktOlusturmaService>.Instance);

        var sonuc = await service.OlusturAsync(TalepFromClaim(claim, _kurumId, eBelgeKaydiId));

        Assert.NotNull(sonuc);
        Assert.Equal(EBelgeArtefaktOlusturmaSonucuTuru.AtomikKaliciHata, sonuc!.SonucTuru);
        Assert.Equal("EBELGE_ARTIFACT_HASH_MISMATCH", sonuc.HataKodu);

        await using var verifyCtx = CreateDbContext();
        Assert.False(await verifyCtx.EBelgeArtifactlari.AnyAsync(a => a.EBelgeKaydiId == eBelgeKaydiId));
    }

    // ---- Tam outbox akışı (handler + işleme servisi ile, gerçek sidecar) (senaryo 18) ----

    [IntegrationFact]
    public async Task TamOutboxAkisiClaimIslemeVeTamamlamaBirlikteCalisir()
    {
        await using var seedCtx = CreateDbContext();
        var eBelgeKaydiId = await SeedEBelgeKaydiWithV2SnapshotAsync(seedCtx);

        seedCtx.EBelgeOutboxMesajlari.Add(new EBelgeOutboxMesaji
        {
            KurumId = _kurumId,
            EBelgeKaydiId = eBelgeKaydiId,
            IsTuru = EBelgeOutboxIsTuru.ArtefaktOlustur,
            Durum = EBelgeOutboxDurumu.Bekliyor,
            DenemeSayisi = 0,
        });
        await seedCtx.SaveChangesAsync();

        await using var workCtx = CreateDbContext();
        var claimService = new EBelgeOutboxClaimLeaseService(workCtx);
        var claim = await claimService.TryClaimNextAsync(TimeSpan.FromSeconds(60));
        Assert.NotNull(claim);
        Assert.Equal(eBelgeKaydiId, claim!.EBelgeKaydiId);

        var artefaktService = CreateService(workCtx);
        var handler = new EBelgeArtefaktOlusturOutboxHandler(artefaktService);
        var transitionService = new EBelgeOutboxLeaseTransitionService(workCtx);
        var retryPolicy = new EBelgeOutboxRetryPolicy();
        var islemeService = new EBelgeOutboxMesajIslemeService(
            [handler], retryPolicy, transitionService, NullLogger<EBelgeOutboxMesajIslemeService>.Instance);

        var sonuc = await islemeService.IsleAsync(claim);

        Assert.Equal(EBelgeOutboxIslemeSonucuTuru.Tamamlandi, sonuc.SonucTuru);

        await using var verifyCtx = CreateDbContext();
        var outbox = await verifyCtx.EBelgeOutboxMesajlari.AsNoTracking().SingleAsync(x => x.EBelgeKaydiId == eBelgeKaydiId);
        Assert.Equal(EBelgeOutboxDurumu.Tamamlandi, outbox.Durum);
        Assert.True(await verifyCtx.EBelgeArtifactlari.AnyAsync(a => a.EBelgeKaydiId == eBelgeKaydiId));

        var kayit = await verifyCtx.EBelgeKayitlari.AsNoTracking().SingleAsync(x => x.Id == eBelgeKaydiId);
        Assert.Equal(EBelgeKaydiDurumu.UnsignedUblHazir, kayit.Durum);
    }

    // ---- Faz 2B.7: imzalama aktivasyon kapısının GERÇEK artefakt-oluşturma akışına bağlanması (md.18) ----

    [IntegrationFact]
    public async Task AktivasyonKapisiAcikkenIlkBasariylaTekBirUblImzalaOutboxMesajiOlusturulur()
    {
        await using var dbContext = CreateDbContext();
        var eBelgeKaydiId = await SeedEBelgeKaydiWithV2SnapshotAsync(dbContext);
        var claim = await SeedAndClaimOutboxAsync(dbContext, eBelgeKaydiId);

        var service = CreateService(dbContext, signingActivationGate: FakeSigningActivationGate.Acik);
        var sonuc = await service.OlusturAsync(TalepFromClaim(claim, _kurumId, eBelgeKaydiId));

        Assert.True(sonuc!.BasariliMi, $"{sonuc.SonucTuru}: {sonuc.HataKodu} {sonuc.HataMesaji}");

        await using var verifyCtx = CreateDbContext();
        var imzalaMesajlari = await verifyCtx.EBelgeOutboxMesajlari.AsNoTracking()
            .Where(x => x.EBelgeKaydiId == eBelgeKaydiId && x.IsTuru == EBelgeOutboxIsTuru.UblImzala)
            .ToListAsync();
        Assert.Single(imzalaMesajlari);
        Assert.Equal(EBelgeOutboxDurumu.Bekliyor, imzalaMesajlari[0].Durum);
    }

    [IntegrationFact]
    public async Task AktivasyonKapisiKapaliykenUblImzalaOutboxMesajiOlusturulmaz()
    {
        await using var dbContext = CreateDbContext();
        var eBelgeKaydiId = await SeedEBelgeKaydiWithV2SnapshotAsync(dbContext);
        var claim = await SeedAndClaimOutboxAsync(dbContext, eBelgeKaydiId);

        // CreateService varsayılanı zaten Kapali'dır (bkz. FakeSigningActivationGate) - açıkça geçirilir.
        var service = CreateService(dbContext, signingActivationGate: FakeSigningActivationGate.Kapali);
        var sonuc = await service.OlusturAsync(TalepFromClaim(claim, _kurumId, eBelgeKaydiId));

        Assert.True(sonuc!.BasariliMi);

        await using var verifyCtx = CreateDbContext();
        Assert.False(await verifyCtx.EBelgeOutboxMesajlari.AnyAsync(x => x.EBelgeKaydiId == eBelgeKaydiId && x.IsTuru == EBelgeOutboxIsTuru.UblImzala));
    }

    [IntegrationFact]
    public async Task AktivasyonKapisiAcikkenIdempotentTekrardaIkinciUblImzalaMesajiOlusturulmaz()
    {
        // Senaryo: gate AÇIK iken İLK çağrıda tam olarak bir UblImzala mesajı oluşur; AYNI
        // (önceden seedli, hash'i eşleşen) idempotent-başarı yolu TEKRAR tetiklense bile - bkz.
        // OnceOnceSeedliHashEslesenMevcutArtefaktIdempotentBasariylaTamamlanirIkinciSatirEklenmez -
        // İKİNCİ bir UblImzala mesajı EKLENMEZ (yalnız İLK GERÇEK oluşturmada tetiklenir, bkz.
        // EBelgeArtefaktOlusturmaService'teki ilgili yorum).
        await using var dbContext = CreateDbContext();
        var eBelgeKaydiId = await SeedEBelgeKaydiWithV2SnapshotAsync(dbContext);

        if (_sidecarFixture.BaseUrl is null)
        {
            Assert.Fail($"Sidecar ayağa kaldırılamadı: {_sidecarFixture.AtlamaNedeni}");
        }

        var v2Snapshot = EBelgeUblRendererTestVerisi.GecerliSnapshot();
        var beklenenRenderSonucu = await RealRendererTestSupport.CreateRealRenderer(_sidecarFixture.BaseUrl!)
            .RenderAsync(v2Snapshot, CancellationToken.None);
        var snapshotEntity = await dbContext.EBelgeSnapshots.AsNoTracking().SingleAsync(x => x.EBelgeKaydiId == eBelgeKaydiId);

        dbContext.EBelgeArtifactlari.Add(new EBelgeArtifact
        {
            KurumId = _kurumId,
            EBelgeKaydiId = eBelgeKaydiId,
            ArtifactTipi = EBelgeArtifactTipi.UblXml,
            ArtifactAsamasi = EBelgeArtifactAsamasi.Unsigned,
            RuleSetId = beklenenRenderSonucu.KuralSetiKimligi,
            SnapshotSchemaVersion = int.Parse(EBelgeCanonicalSnapshotV2Reader.SupportedSnapshotSchemaVersion),
            KaynakSnapshotSha256 = snapshotEntity.CanonicalSha256,
            ArtifactSha256 = beklenenRenderSonucu.UnsignedUblSha256,
            Icerik = beklenenRenderSonucu.UnsignedUblUtf8.ToArray(),
            MimeType = "application/xml",
            DosyaAdi = "onceden-seedli.xml",
            OlusturulmaZamaniUtc = DateTime.UtcNow,
        });
        await dbContext.SaveChangesAsync();

        var claim = await SeedAndClaimOutboxAsync(dbContext, eBelgeKaydiId);
        var service = CreateService(dbContext, signingActivationGate: FakeSigningActivationGate.Acik);
        var sonuc = await service.OlusturAsync(TalepFromClaim(claim, _kurumId, eBelgeKaydiId));

        Assert.Equal(EBelgeArtefaktOlusturmaSonucuTuru.AtomikBasarili, sonuc!.SonucTuru);

        await using var verifyCtx = CreateDbContext();
        var imzalaMesajSayisi = await verifyCtx.EBelgeOutboxMesajlari.CountAsync(x => x.EBelgeKaydiId == eBelgeKaydiId && x.IsTuru == EBelgeOutboxIsTuru.UblImzala);
        Assert.Equal(0, imzalaMesajSayisi); // idempotent (önceden seedli) tamamlanma - YENİ mesaj EKLENMEDİ
    }

    // ---- Okuma servisi ----

    [IntegrationFact]
    public async Task ArtifactServiceGecerliArtefaktiTenantSiniriIleDoner()
    {
        await using var dbContext = CreateDbContext();
        var eBelgeKaydiId = await SeedEBelgeKaydiWithV2SnapshotAsync(dbContext);
        var claim = await SeedAndClaimOutboxAsync(dbContext, eBelgeKaydiId);
        var service = CreateService(dbContext);
        var sonuc = await service.OlusturAsync(TalepFromClaim(claim, _kurumId, eBelgeKaydiId));
        Assert.True(sonuc!.BasariliMi);

        await using var readCtx = CreateDbContext();
        var artifactService = new EBelgeArtifactService(readCtx);

        var dogru = await artifactService.GetUnsignedUblAsync(_kurumId, eBelgeKaydiId);
        Assert.NotNull(dogru);
        Assert.Equal("application/xml", dogru!.MimeType);

        var yanlisTenant = await artifactService.GetUnsignedUblAsync(_kurumId + 999_000, eBelgeKaydiId);
        Assert.Null(yanlisTenant);
    }

    // ---- Geçici hatalar (senaryo 11) ----

    [IntegrationFact]
    public async Task SidecarErisilemiyorsaGeciciHataOlurArtefaktOlusmazVeSahiplikKontroluGerekmez()
    {
        await using var dbContext = CreateDbContext();
        var eBelgeKaydiId = await SeedEBelgeKaydiWithV2SnapshotAsync(dbContext);

        var kuralSeti = EBelgeUblRendererTestVerisi.KuralSetiYukle();
        var xsdValidator = new EBelgeUblXsdValidator(kuralSeti);
        // Kasıtlı olarak GERÇEK ama HİÇBİR ŞEYİN DİNLEMEDİĞİ bir port - gerçek bağlantı reddi
        // üretir (mock DEĞİL, gerçek TCP bağlantı hatası).
        var http = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:1"), Timeout = TimeSpan.FromSeconds(3) };
        var schematronValidator = new SaxonSidecarEBelgeSchematronValidator(http);
        var renderer = new EBelgeUblRenderer(kuralSeti, xsdValidator, schematronValidator);
        var service = new EBelgeArtefaktOlusturmaService(
            dbContext, new EBelgeCanonicalSnapshotV2Reader(), renderer,
            new EBelgeOutboxLeaseTransitionService(dbContext), FakeSigningActivationGate.Kapali, TimeProvider.System,
            NullLogger<EBelgeArtefaktOlusturmaService>.Instance);

        // Geçici hata yolu (Gecici) EBelgeKaydi'yı hiç DEĞİŞTİRMEDİĞİNDEN, atomik transaction/lease
        // sahiplik doğrulaması GEREKMEZ (bkz. görev md.5) - bu yüzden talep KASITLI OLARAK gerçek
        // (claim edilmiş) OLMAYAN ama YİNE DE şekil olarak GEÇERLİ (pozitif OutboxMesajiId, GUID
        // formatlı token - bkz. md.5 "talep modelini doğrula") bir OutboxMesajiId/KilitToken taşır
        // ve yine de aynı sonuca ulaşılmalıdır - bu, ownership/DB'ye HİÇ dokunulmadığını kanıtlar.
        var talep = new EBelgeArtefaktOlusturmaTalebi(_kurumId, eBelgeKaydiId, 999_999_999, Guid.NewGuid().ToString("D"), DateTime.UtcNow.AddMinutes(5));
        var sonuc = await service.OlusturAsync(talep);

        Assert.NotNull(sonuc);
        Assert.Equal(EBelgeArtefaktOlusturmaSonucuTuru.GeciciHata, sonuc!.SonucTuru);
        Assert.Equal(EBelgeUblSchematronServiceUnavailableException.SafeErrorCode, sonuc.HataKodu);

        await using var verifyCtx = CreateDbContext();
        Assert.False(await verifyCtx.EBelgeArtifactlari.AnyAsync(a => a.EBelgeKaydiId == eBelgeKaydiId));
        var kayit = await verifyCtx.EBelgeKayitlari.AsNoTracking().SingleAsync(x => x.Id == eBelgeKaydiId);
        Assert.Equal(EBelgeKaydiDurumu.SnapshotHazir, kayit.Durum); // geçici hatada EBelgeKaydi.Durum DEĞİŞMEZ
    }

    // ---- İdempotency çakışması / soft-delete (senaryo 12, 13) ----

    [IntegrationFact]
    public async Task FarkliHashliMevcutArtefaktAtomikIdempotencyConflictUretir()
    {
        await using var dbContext = CreateDbContext();
        var eBelgeKaydiId = await SeedEBelgeKaydiWithV2SnapshotAsync(dbContext);

        // Aynı benzersiz anahtar altında, KASITLI OLARAK farklı bir hash'e sahip "yabancı" bir
        // artefakt önceden var - gerçek render sonucu bununla eşleşmeyecek.
        dbContext.EBelgeArtifactlari.Add(new EBelgeArtifact
        {
            KurumId = _kurumId,
            EBelgeKaydiId = eBelgeKaydiId,
            ArtifactTipi = EBelgeArtifactTipi.UblXml,
            ArtifactAsamasi = EBelgeArtifactAsamasi.Unsigned,
            RuleSetId = "farkli-kural-seti",
            SnapshotSchemaVersion = 2,
            KaynakSnapshotSha256 = new string('a', 64),
            ArtifactSha256 = new string('b', 64),
            Icerik = "<farkli/>"u8.ToArray(),
            MimeType = "application/xml",
            DosyaAdi = "farkli.xml",
            OlusturulmaZamaniUtc = DateTime.UtcNow,
        });
        await dbContext.SaveChangesAsync();

        var claim = await SeedAndClaimOutboxAsync(dbContext, eBelgeKaydiId);
        var service = CreateService(dbContext);
        var sonuc = await service.OlusturAsync(TalepFromClaim(claim, _kurumId, eBelgeKaydiId));

        Assert.NotNull(sonuc);
        Assert.Equal(EBelgeArtefaktOlusturmaSonucuTuru.AtomikKaliciHata, sonuc!.SonucTuru);
        Assert.Equal(EBelgeArtifactIdempotencyConflictException.SafeErrorCode, sonuc.HataKodu);

        await using var verifyCtx = CreateDbContext();
        var sayi = await verifyCtx.EBelgeArtifactlari.CountAsync(a => a.EBelgeKaydiId == eBelgeKaydiId);
        Assert.Equal(1, sayi); // ikinci (rakip) satır EKLENMEDİ - yalnız orijinal "yabancı" satır kaldı

        // AtomikKaliciHata sonucunun BAŞARIYLA dönmesi (exception FIRLATILMADAN), bu yolun ARTIK
        // rollback edilmiş-ama-dispose-edilmemiş bir transaction üzerinde ikinci bir
        // BeginTransactionAsync çağrısı YAPMADIĞINI da yapısal olarak kanıtlar - eski (hatalı)
        // akışta bu senaryo bir InvalidOperationException riski taşıyordu (bkz. Faz 2B.6.2 görev
        // md.3-4, senaryo 9).
        var kayit = await verifyCtx.EBelgeKayitlari.AsNoTracking().SingleAsync(x => x.Id == eBelgeKaydiId);
        Assert.Equal(EBelgeKaydiDurumu.UnsignedUblKaliciHata, kayit.Durum);
    }

    [IntegrationFact]
    public async Task SoftDeleteEdilmisMevcutArtefaktAtomikIdempotencyConflictUretirTekrarDenemeAtanmaz()
    {
        await using var dbContext = CreateDbContext();
        var eBelgeKaydiId = await SeedEBelgeKaydiWithV2SnapshotAsync(dbContext);

        // Önce (aynı benzersiz anahtarlı) bir artefakt eklenir, ardından - EF'in normal
        // Modified/Deleted yollarını BİLEREK atlayarak (EBelgeArtifact immutable - bkz.
        // StysAppDbContext.ApplyAuditInfo) - DOĞRUDAN SQL ile soft-delete edilir. Mali/yasal
        // artefaktlar SİLİNEMEZ sözleşmesi nedeniyle bu durum veri bütünlüğü ihlali sayılır
        // (bkz. görev md.6) - sessiz başarı YOK.
        dbContext.EBelgeArtifactlari.Add(new EBelgeArtifact
        {
            KurumId = _kurumId,
            EBelgeKaydiId = eBelgeKaydiId,
            ArtifactTipi = EBelgeArtifactTipi.UblXml,
            ArtifactAsamasi = EBelgeArtifactAsamasi.Unsigned,
            RuleSetId = "eski-kural-seti",
            SnapshotSchemaVersion = 2,
            KaynakSnapshotSha256 = new string('a', 64),
            ArtifactSha256 = new string('b', 64),
            Icerik = "<eski/>"u8.ToArray(),
            MimeType = "application/xml",
            DosyaAdi = "eski.xml",
            OlusturulmaZamaniUtc = DateTime.UtcNow,
        });
        await dbContext.SaveChangesAsync();
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [muhasebe].[EBelgeArtifactlari] SET [IsDeleted] = 1 WHERE [EBelgeKaydiId] = {eBelgeKaydiId}");

        var claim = await SeedAndClaimOutboxAsync(dbContext, eBelgeKaydiId);
        var service = CreateService(dbContext);
        var sonuc = await service.OlusturAsync(TalepFromClaim(claim, _kurumId, eBelgeKaydiId));

        Assert.NotNull(sonuc);
        Assert.Equal(EBelgeArtefaktOlusturmaSonucuTuru.AtomikKaliciHata, sonuc!.SonucTuru);
        Assert.Equal(EBelgeArtifactIdempotencyConflictException.SafeErrorCode, sonuc.HataKodu);

        await using var verifyCtx = CreateDbContext();
        // IgnoreQueryFilters KULLANILMADAN normal sorgu, soft-delete edilmiş satırı zaten
        // GÖRMEZ - global filtre olmadan da tekrar sayarak "yeni satır eklenmedi"ği doğrulanır.
        var sayiFiltresiz = await verifyCtx.EBelgeArtifactlari.IgnoreQueryFilters().CountAsync(a => a.EBelgeKaydiId == eBelgeKaydiId);
        Assert.Equal(1, sayiFiltresiz); // yeni satır EKLENMEDİ

        var outbox = await verifyCtx.EBelgeOutboxMesajlari.AsNoTracking().SingleAsync(x => x.Id == claim.OutboxMesajiId);
        Assert.Equal(EBelgeOutboxDurumu.Hata, outbox.Durum);
        Assert.Null(outbox.SonrakiDenemeZamaniUtc); // KALICI - geçici retry ATANMADI (senaryo 12)

        // Outbox VE EBelgeKaydi, AYNI atomik transaction'da BİRLİKTE güncellenmiştir (senaryo 11).
        var kayit = await verifyCtx.EBelgeKayitlari.AsNoTracking().SingleAsync(x => x.Id == eBelgeKaydiId);
        Assert.Equal(EBelgeKaydiDurumu.UnsignedUblKaliciHata, kayit.Durum);
    }
}
