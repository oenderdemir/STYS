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
using Xunit;

namespace STYS.Tests;

/// <summary>
/// Faz 2B.6 - GERÇEK SQL Server'a karşı, GERÇEK renderer + GERÇEK Java Saxon sidecar ile
/// EBelgeArtefaktOlusturmaService'in outbox tüketim akışını doğrular (bkz. görev md.17,
/// "gerçek sidecar gereken en az bir outbox integration testi"). Snapshot içeriği, Faz 2B.5'te
/// zaten uçtan uca doğrulanmış EBelgeUblRendererTestVerisi.GecerliSnapshot()'tan türetilir -
/// snapshot ÜRETİMİ burada test EDİLMEZ (ayrı, önceki fazlarda test edildi); burada test edilen
/// KONU, VAROLAN bir immutable snapshot kaydından artefakt üretme/kalıcılaştırma akışıdır.
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
        // BİLMEZ, bu yüzden artefaktlar ÖNCE burada, doğrudan silinir.
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

    private EBelgeArtefaktOlusturmaService CreateService(StysAppDbContext dbContext)
    {
        if (_sidecarFixture.BaseUrl is null)
        {
            Assert.Fail($"Sidecar ayağa kaldırılamadı: {_sidecarFixture.AtlamaNedeni}");
        }

        return new EBelgeArtefaktOlusturmaService(
            dbContext,
            new EBelgeCanonicalSnapshotV2Reader(),
            RealRendererTestSupport.CreateRealRenderer(_sidecarFixture.BaseUrl!),
            NullLogger<EBelgeArtefaktOlusturmaService>.Instance);
    }

    // ---- Başarılı akış ----

    [IntegrationFact]
    public async Task GercekV2SnapshotGercekSidecarIleArtefaktUretirVeHashZinciriDogrulanir()
    {
        await using var dbContext = CreateDbContext();
        var eBelgeKaydiId = await SeedEBelgeKaydiWithV2SnapshotAsync(dbContext);

        var service = CreateService(dbContext);
        var sonuc = await service.OlusturAsync(new EBelgeArtefaktOlusturmaTalebi(_kurumId, eBelgeKaydiId));

        Assert.NotNull(sonuc);
        Assert.True(sonuc!.BasariliMi, $"{sonuc.SonucTuru}: {sonuc.HataKodu} {sonuc.HataMesaji}");

        await using var verifyCtx = CreateDbContext();
        var artifact = await verifyCtx.EBelgeArtifactlari.AsNoTracking().SingleAsync(a => a.EBelgeKaydiId == eBelgeKaydiId);
        var kayit = await verifyCtx.EBelgeKayitlari.AsNoTracking().SingleAsync(x => x.Id == eBelgeKaydiId);
        var snapshot = await verifyCtx.EBelgeSnapshots.AsNoTracking().SingleAsync(x => x.EBelgeKaydiId == eBelgeKaydiId);

        Assert.Equal(EBelgeKaydiDurumu.UnsignedUblHazir, kayit.Durum);
        Assert.Equal(snapshot.CanonicalSha256, artifact.KaynakSnapshotSha256);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(artifact.Icerik)), artifact.ArtifactSha256);
        Assert.Equal("application/xml", artifact.MimeType);
        Assert.EndsWith(".xml", artifact.DosyaAdi, StringComparison.Ordinal);
        Assert.DoesNotContain("/", artifact.DosyaAdi, StringComparison.Ordinal);
        Assert.DoesNotContain("\\", artifact.DosyaAdi, StringComparison.Ordinal);
    }

    [IntegrationFact]
    public async Task IdempotentTekrarIslemeIkinciArtefaktOlusturmaz()
    {
        await using var dbContext = CreateDbContext();
        var eBelgeKaydiId = await SeedEBelgeKaydiWithV2SnapshotAsync(dbContext);

        var service1 = CreateService(dbContext);
        var sonuc1 = await service1.OlusturAsync(new EBelgeArtefaktOlusturmaTalebi(_kurumId, eBelgeKaydiId));
        Assert.True(sonuc1!.BasariliMi);

        await using var dbContext2 = CreateDbContext();
        var service2 = CreateService(dbContext2);
        var sonuc2 = await service2.OlusturAsync(new EBelgeArtefaktOlusturmaTalebi(_kurumId, eBelgeKaydiId));
        Assert.True(sonuc2!.BasariliMi);

        await using var verifyCtx = CreateDbContext();
        var sayi = await verifyCtx.EBelgeArtifactlari.CountAsync(a => a.EBelgeKaydiId == eBelgeKaydiId);
        Assert.Equal(1, sayi);
    }

    [IntegrationFact]
    public async Task IkiParalelIstekTekArtefaktUretir()
    {
        await using var seedCtx = CreateDbContext();
        var eBelgeKaydiId = await SeedEBelgeKaydiWithV2SnapshotAsync(seedCtx);

        await using var ctx1 = CreateDbContext();
        await using var ctx2 = CreateDbContext();
        var service1 = CreateService(ctx1);
        var service2 = CreateService(ctx2);

        var talep = new EBelgeArtefaktOlusturmaTalebi(_kurumId, eBelgeKaydiId);
        var sonuclar = await Task.WhenAll(
            service1.OlusturAsync(talep),
            service2.OlusturAsync(talep));

        Assert.All(sonuclar, s => Assert.True(s!.BasariliMi, $"{s!.SonucTuru}: {s.HataKodu} {s.HataMesaji}"));

        await using var verifyCtx = CreateDbContext();
        var sayi = await verifyCtx.EBelgeArtifactlari.CountAsync(a => a.EBelgeKaydiId == eBelgeKaydiId);
        Assert.Equal(1, sayi);
    }

    // ---- Kalıcı hatalar ----

    [IntegrationFact]
    public async Task DesteklenmeyenSnapshotSemaSurumuKaliciHataOlurArtefaktOlusmaz()
    {
        await using var dbContext = CreateDbContext();
        var satisBelgesiId = await CreateSatisBelgesiIdAsync(dbContext);

        await using var seedCtx = CreateDbContext();
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

        // V1 benzeri, geçersiz/desteklenmeyen bir "canonical" gövde - şema sürümü "2" değil.
        const string v1BenzeriJson = "{\"surum\":\"1\"}";
        var v1Bytes = Encoding.UTF8.GetBytes(v1BenzeriJson);
        seedCtx.EBelgeSnapshots.Add(new EBelgeSnapshot
        {
            KurumId = _kurumId,
            EBelgeKaydiId = eBelgeKaydi.Id,
            BelgeVersiyonu = 1,
            SnapshotSchemaVersion = "1",
            CanonicalJson = v1BenzeriJson,
            CanonicalSha256 = Convert.ToHexString(SHA256.HashData(v1Bytes)),
        });
        await seedCtx.SaveChangesAsync();

        var service = CreateService(seedCtx);
        var sonuc = await service.OlusturAsync(new EBelgeArtefaktOlusturmaTalebi(_kurumId, eBelgeKaydi.Id));

        Assert.NotNull(sonuc);
        Assert.Equal(EBelgeArtefaktOlusturmaSonucuTuru.KaliciHata, sonuc!.SonucTuru);

        await using var verifyCtx = CreateDbContext();
        Assert.False(await verifyCtx.EBelgeArtifactlari.AnyAsync(a => a.EBelgeKaydiId == eBelgeKaydi.Id));
    }

    [IntegrationFact]
    public async Task SnapshotHashUyusmazligiKaliciHataOlur()
    {
        await using var dbContext = CreateDbContext();
        var satisBelgesiId = await CreateSatisBelgesiIdAsync(dbContext);

        await using var seedCtx = CreateDbContext();
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

        var v2Snapshot = EBelgeUblRendererTestVerisi.GecerliSnapshot();
        var utf8Bytes = JsonSerializer.SerializeToUtf8Bytes(v2Snapshot, EBelgeCanonicalSnapshotV2Reader.CanonicalJsonOptions);
        var json = Encoding.UTF8.GetString(utf8Bytes);

        seedCtx.EBelgeSnapshots.Add(new EBelgeSnapshot
        {
            KurumId = _kurumId,
            EBelgeKaydiId = eBelgeKaydi.Id,
            BelgeVersiyonu = 1,
            SnapshotSchemaVersion = EBelgeCanonicalSnapshotV2Reader.SupportedSnapshotSchemaVersion,
            CanonicalJson = json,
            CanonicalSha256 = new string('0', 64), // kasıtlı olarak YANLIŞ hash
        });
        await seedCtx.SaveChangesAsync();

        var service = CreateService(seedCtx);
        var sonuc = await service.OlusturAsync(new EBelgeArtefaktOlusturmaTalebi(_kurumId, eBelgeKaydi.Id));

        Assert.NotNull(sonuc);
        Assert.Equal(EBelgeArtefaktOlusturmaSonucuTuru.KaliciHata, sonuc!.SonucTuru);
        Assert.Equal(EBelgeCanonicalSnapshotException.SafeErrorCode, sonuc.HataKodu);
    }

    [IntegrationFact]
    public async Task EBelgeKaydiBulunamazsaKaliciHataOlur()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var sonuc = await service.OlusturAsync(new EBelgeArtefaktOlusturmaTalebi(_kurumId, int.MaxValue - 1));

        Assert.NotNull(sonuc);
        Assert.Equal(EBelgeArtefaktOlusturmaSonucuTuru.KaliciHata, sonuc!.SonucTuru);
        Assert.Equal("EBELGE_KAYDI_BULUNAMADI", sonuc.HataKodu);
    }

    [IntegrationFact]
    public async Task YanlisKurumIdIleEBelgeKaydiBulunamaz()
    {
        await using var dbContext = CreateDbContext();
        var eBelgeKaydiId = await SeedEBelgeKaydiWithV2SnapshotAsync(dbContext);

        var service = CreateService(dbContext);
        var sonuc = await service.OlusturAsync(new EBelgeArtefaktOlusturmaTalebi(_kurumId + 999_000, eBelgeKaydiId));

        Assert.NotNull(sonuc);
        Assert.Equal(EBelgeArtefaktOlusturmaSonucuTuru.KaliciHata, sonuc!.SonucTuru);
        Assert.Equal("EBELGE_KAYDI_BULUNAMADI", sonuc.HataKodu);
    }

    // ---- Tam outbox akışı (handler + işleme servisi ile) ----

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

    // ---- Okuma servisi ----

    [IntegrationFact]
    public async Task ArtifactServiceGecerliArtefaktiTenantSiniriIleDoner()
    {
        await using var dbContext = CreateDbContext();
        var eBelgeKaydiId = await SeedEBelgeKaydiWithV2SnapshotAsync(dbContext);
        var service = CreateService(dbContext);
        var sonuc = await service.OlusturAsync(new EBelgeArtefaktOlusturmaTalebi(_kurumId, eBelgeKaydiId));
        Assert.True(sonuc!.BasariliMi);

        await using var readCtx = CreateDbContext();
        var artifactService = new EBelgeArtifactService(readCtx);

        var dogru = await artifactService.GetUnsignedUblAsync(_kurumId, eBelgeKaydiId);
        Assert.NotNull(dogru);
        Assert.Equal("application/xml", dogru!.MimeType);

        var yanlisTenant = await artifactService.GetUnsignedUblAsync(_kurumId + 999_000, eBelgeKaydiId);
        Assert.Null(yanlisTenant);
    }

    // ---- Geçici hatalar ----

    [IntegrationFact]
    public async Task SidecarErisilemiyorsaGeciciHataOlurArtefaktOlusmaz()
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
            dbContext, new EBelgeCanonicalSnapshotV2Reader(), renderer, NullLogger<EBelgeArtefaktOlusturmaService>.Instance);

        var sonuc = await service.OlusturAsync(new EBelgeArtefaktOlusturmaTalebi(_kurumId, eBelgeKaydiId));

        Assert.NotNull(sonuc);
        Assert.Equal(EBelgeArtefaktOlusturmaSonucuTuru.GeciciHata, sonuc!.SonucTuru);
        Assert.Equal(EBelgeUblSchematronServiceUnavailableException.SafeErrorCode, sonuc.HataKodu);

        await using var verifyCtx = CreateDbContext();
        Assert.False(await verifyCtx.EBelgeArtifactlari.AnyAsync(a => a.EBelgeKaydiId == eBelgeKaydiId));
        var kayit = await verifyCtx.EBelgeKayitlari.AsNoTracking().SingleAsync(x => x.Id == eBelgeKaydiId);
        Assert.Equal(EBelgeKaydiDurumu.SnapshotHazir, kayit.Durum); // geçici hatada EBelgeKaydi.Durum DEĞİŞMEZ
    }

    // ---- İdempotency çakışması ----

    [IntegrationFact]
    public async Task FarkliHashliMevcutArtefaktIdempotencyConflictUretir()
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

        var service = CreateService(dbContext);
        var sonuc = await service.OlusturAsync(new EBelgeArtefaktOlusturmaTalebi(_kurumId, eBelgeKaydiId));

        Assert.NotNull(sonuc);
        Assert.Equal(EBelgeArtefaktOlusturmaSonucuTuru.KaliciHata, sonuc!.SonucTuru);
        Assert.Equal(EBelgeArtifactIdempotencyConflictException.SafeErrorCode, sonuc.HataKodu);

        await using var verifyCtx = CreateDbContext();
        var sayi = await verifyCtx.EBelgeArtifactlari.CountAsync(a => a.EBelgeKaydiId == eBelgeKaydiId);
        Assert.Equal(1, sayi); // ikinci (rakip) satır EKLENMEDİ - yalnız orijinal "yabancı" satır kaldı
    }
}
