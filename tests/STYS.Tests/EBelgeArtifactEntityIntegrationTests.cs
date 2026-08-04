using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.SatisBelgeleri;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Muhasebe.SatisBelgeleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using TOD.Platform.SharedKernel.Exceptions;
using Xunit;

namespace STYS.Tests;

/// <summary>Faz 2B.6 - EBelgeArtifact entity/migration invariantlarını (immutability, benzersizlik, cross-tenant FK, Restrict delete) GERÇEK SQL Server'a karşı doğrular.</summary>
[Trait("Category", "Integration")]
[Collection(SqlServerIntegrationCollection.Name)]
public class EBelgeArtifactEntityIntegrationTests : IAsyncLifetime
{
    private const string TestMarker = "EBO-2B6-ENT";

    private string _uniqueSuffix = TestMarker;
    private int _kurumId;
    private int _ilId;
    private int _tesisId;
    private int _musteriKartId;
    private int _kurumId2;
    private int _ilId2;
    private int _tesisId2;

    public async Task InitializeAsync()
    {
        _uniqueSuffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];

        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var (kurum, il, tesis) = await SatisBelgesiMuhasebeTestSupport.SeedKurumIlTesisAsync(dbContext, _uniqueSuffix);
        _kurumId = kurum.Id;
        _ilId = il.Id;
        _tesisId = tesis.Id;

        var (kurum2, il2, tesis2) = await SatisBelgesiMuhasebeTestSupport.SeedKurumIlTesisAsync(dbContext, _uniqueSuffix + "-B");
        _kurumId2 = kurum2.Id;
        _ilId2 = il2.Id;
        _tesisId2 = tesis2.Id;

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
            $"DELETE FROM [muhasebe].[EBelgeArtifactlari] WHERE [KurumId] = {_kurumId} OR [KurumId] = {_kurumId2}");
        await SatisBelgesiMuhasebeTestSupport.CleanupAsync(dbContext, _uniqueSuffix, _tesisId, _kurumId, _ilId);
        await SatisBelgesiMuhasebeTestSupport.CleanupAsync(dbContext, _uniqueSuffix + "-B", _tesisId2, _kurumId2, _ilId2);
    }

    private static StysAppDbContext CreateDbContext() => SatisBelgesiMuhasebeTestSupport.CreateDbContext();

    private async Task<int> CreateSatisBelgesiIdAsync(StysAppDbContext dbContext, int kurumId, int tesisId, int cariKartId)
    {
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
        var created = await service.CreateAsync(new CreateSatisBelgesiRequest
        {
            BelgeNo = $"{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
            BelgeTipi = SatisBelgesiTipi.SatisFaturasi,
            TesisId = tesisId,
            CariKartId = cariKartId,
            BelgeTarihi = new DateTime(2026, 7, 1),
            Satirlar =
            [
                new CreateSatisBelgesiSatiriRequest
                {
                    SiraNo = 1,
                    Aciklama = "Test satiri",
                    SatirTipi = SatisBelgesiSatirTipi.EkHizmet,
                    Miktar = 1,
                    BirimFiyat = 100m,
                    KdvUygulamaTipi = (int)STYS.Muhasebe.Kdv.Enums.KdvUygulamaTipi.Kdvli,
                    KdvOrani = 20m
                }
            ]
        });
        return created.Id!.Value;
    }

    private async Task<int> SeedEBelgeKaydiAsync(StysAppDbContext dbContext, int kurumId, int satisBelgesiId)
    {
        var v2Snapshot = EBelgeUblRendererTestVerisi.GecerliSnapshot();
        var utf8Bytes = JsonSerializer.SerializeToUtf8Bytes(v2Snapshot, EBelgeCanonicalSnapshotV2Reader.CanonicalJsonOptions);

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

        dbContext.EBelgeSnapshots.Add(new EBelgeSnapshot
        {
            KurumId = kurumId,
            EBelgeKaydiId = eBelgeKaydi.Id,
            BelgeVersiyonu = 1,
            SnapshotSchemaVersion = EBelgeCanonicalSnapshotV2Reader.SupportedSnapshotSchemaVersion,
            CanonicalJson = System.Text.Encoding.UTF8.GetString(utf8Bytes),
            CanonicalSha256 = Convert.ToHexString(SHA256.HashData(utf8Bytes)),
        });
        await dbContext.SaveChangesAsync();

        return eBelgeKaydi.Id;
    }

    private static EBelgeArtifact BuildArtifact(int kurumId, int eBelgeKaydiId, string suffix = "") => new()
    {
        KurumId = kurumId,
        EBelgeKaydiId = eBelgeKaydiId,
        ArtifactTipi = EBelgeArtifactTipi.UblXml,
        ArtifactAsamasi = EBelgeArtifactAsamasi.Unsigned,
        RuleSetId = "test-kural-seti",
        SnapshotSchemaVersion = 2,
        KaynakSnapshotSha256 = new string('a', 64),
        ArtifactSha256 = new string('b', 64),
        Icerik = System.Text.Encoding.UTF8.GetBytes($"<test{suffix}/>"),
        MimeType = "application/xml",
        DosyaAdi = $"test{suffix}.xml",
        OlusturulmaZamaniUtc = DateTime.UtcNow,
    };

    [IntegrationFact]
    public async Task ArtefaktBasariylaKaydedilirVeByteBirebirKorunur()
    {
        await using var dbContext = CreateDbContext();
        var satisBelgesiId = await CreateSatisBelgesiIdAsync(dbContext, _kurumId, _tesisId, _musteriKartId);
        var eBelgeKaydiId = await SeedEBelgeKaydiAsync(dbContext, _kurumId, satisBelgesiId);

        var icerik = System.Text.Encoding.UTF8.GetBytes("<Invoice>İçerik Türkçe karakter Ğ Ş Ç Ö Ü İ</Invoice>");
        var artifact = BuildArtifact(_kurumId, eBelgeKaydiId);
        artifact.Icerik = icerik;
        artifact.ArtifactSha256 = Convert.ToHexString(SHA256.HashData(icerik));

        dbContext.EBelgeArtifactlari.Add(artifact);
        await dbContext.SaveChangesAsync();

        await using var verifyCtx = CreateDbContext();
        var okunan = await verifyCtx.EBelgeArtifactlari.AsNoTracking().SingleAsync(a => a.Id == artifact.Id);
        Assert.Equal(icerik, okunan.Icerik);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(okunan.Icerik)), okunan.ArtifactSha256);
    }

    [IntegrationFact]
    public async Task AyniBelgeAsamaIcinIkinciArtefaktDbTarafindanReddedilir()
    {
        await using var dbContext = CreateDbContext();
        var satisBelgesiId = await CreateSatisBelgesiIdAsync(dbContext, _kurumId, _tesisId, _musteriKartId);
        var eBelgeKaydiId = await SeedEBelgeKaydiAsync(dbContext, _kurumId, satisBelgesiId);

        dbContext.EBelgeArtifactlari.Add(BuildArtifact(_kurumId, eBelgeKaydiId, "-1"));
        await dbContext.SaveChangesAsync();

        await using var ikinciCtx = CreateDbContext();
        ikinciCtx.EBelgeArtifactlari.Add(BuildArtifact(_kurumId, eBelgeKaydiId, "-2"));

        await Assert.ThrowsAsync<DbUpdateException>(() => ikinciCtx.SaveChangesAsync());
    }

    [IntegrationFact]
    public async Task SoftDeleteEdilmisArtefaktOlsaBileDuplicateOlusturulamaz()
    {
        await using var dbContext = CreateDbContext();
        var satisBelgesiId = await CreateSatisBelgesiIdAsync(dbContext, _kurumId, _tesisId, _musteriKartId);
        var eBelgeKaydiId = await SeedEBelgeKaydiAsync(dbContext, _kurumId, satisBelgesiId);

        var artifact = BuildArtifact(_kurumId, eBelgeKaydiId, "-1");
        dbContext.EBelgeArtifactlari.Add(artifact);
        await dbContext.SaveChangesAsync();

        // Soft-delete: ApplyAuditInfo EF ÜZERİNDEN Modified/Deleted durumunu engeller (bkz.
        // md.4, immutable sözleşme) - bu yüzden bilinçli olarak ham SQL ile işaretlenir (bu,
        // EF dışı - ör. arşivleme job'u - bir senaryoyu temsil eder).
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [muhasebe].[EBelgeArtifactlari] SET [IsDeleted] = 1 WHERE [Id] = {artifact.Id}");

        await using var ikinciCtx = CreateDbContext();
        ikinciCtx.EBelgeArtifactlari.Add(BuildArtifact(_kurumId, eBelgeKaydiId, "-2"));

        // Benzersizlik indeksi FİLTRESİZDİR - soft-delete edilmiş satır bile rezervasyonu korur.
        await Assert.ThrowsAsync<DbUpdateException>(() => ikinciCtx.SaveChangesAsync());
    }

    [IntegrationFact]
    public async Task CrossTenantArtifactFkDbTarafindanReddedilir()
    {
        await using var dbContext = CreateDbContext();
        var satisBelgesiId = await CreateSatisBelgesiIdAsync(dbContext, _kurumId, _tesisId, _musteriKartId);
        var eBelgeKaydiId = await SeedEBelgeKaydiAsync(dbContext, _kurumId, satisBelgesiId);

        // KurumId2, gerçek eBelgeKaydiId'nin sahibi OLMAYAN başka bir tenant - (EBelgeKaydiId, KurumId)
        // çifti EBelgeKayitlari'nın (Id, KurumId) alternate key'iyle EŞLEŞMEZ.
        var yabanciTenantArtifact = BuildArtifact(_kurumId2, eBelgeKaydiId);

        dbContext.EBelgeArtifactlari.Add(yabanciTenantArtifact);

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    [IntegrationFact]
    public async Task EBelgeKaydiSilmeArtifactNedeniyleRestrictReddedilir()
    {
        await using var dbContext = CreateDbContext();
        var satisBelgesiId = await CreateSatisBelgesiIdAsync(dbContext, _kurumId, _tesisId, _musteriKartId);
        var eBelgeKaydiId = await SeedEBelgeKaydiAsync(dbContext, _kurumId, satisBelgesiId);

        dbContext.EBelgeArtifactlari.Add(BuildArtifact(_kurumId, eBelgeKaydiId));
        await dbContext.SaveChangesAsync();

        // EBelgeKaydi'yi (ve bağlı SatisBelgesi'yi) DOĞRUDAN, ham SQL ile silmeyi dene - FK
        // Restrict olduğundan artefakt varken bu REDDEDİLMELİDİR (cascade YOKTUR).
        await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() =>
            dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM [muhasebe].[EBelgeKayitlari] WHERE [Id] = {eBelgeKaydiId}"));

        // Temizlik: bu testin kendi artefaktını sil ki DisposeAsync'teki genel temizlik akışı bozulmasın.
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM [muhasebe].[EBelgeArtifactlari] WHERE [EBelgeKaydiId] = {eBelgeKaydiId}");
    }

    [IntegrationFact]
    public async Task ArtefaktGuncellemeVeyaSilmeUygulamaSeviyesindeReddedilir()
    {
        await using var dbContext = CreateDbContext();
        var satisBelgesiId = await CreateSatisBelgesiIdAsync(dbContext, _kurumId, _tesisId, _musteriKartId);
        var eBelgeKaydiId = await SeedEBelgeKaydiAsync(dbContext, _kurumId, satisBelgesiId);

        var artifact = BuildArtifact(_kurumId, eBelgeKaydiId);
        dbContext.EBelgeArtifactlari.Add(artifact);
        await dbContext.SaveChangesAsync();

        artifact.DosyaAdi = "degistirilmis.xml";
        await Assert.ThrowsAsync<BaseException>(() => dbContext.SaveChangesAsync());
    }
}
