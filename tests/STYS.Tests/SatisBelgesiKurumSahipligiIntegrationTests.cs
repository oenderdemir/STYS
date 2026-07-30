using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Muhasebe.SatisBelgeleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using STYS.Muhasebe.SatisBelgeleri.Services;
using TOD.Platform.SharedKernel.Exceptions;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// SatisBelgesi'nin ITenantEntity olarak kurum sahipliği kazanmasını (KurumId'nin otoriter olarak
/// TesisId -> Tesis.KurumId zincirinden atanması, istemciden asla alınmaması, güncellenememesi ve
/// StysAppDbContext'in mevcut kurum sorgu filtresi/ApplyTenantRules altyapısının bu belge tipine de
/// GERÇEK SQL Server üzerinde doğru şekilde uygulandığını) doğrulayan entegrasyon testleri.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SqlServerIntegrationCollection.Name)]
public class SatisBelgesiKurumSahipligiIntegrationTests : IAsyncLifetime
{
    private const string TestMarker = "KURUMOWN-772";

    private string _uniqueSuffix = TestMarker;
    private int _kurumAId;
    private int _ilAId;
    private int _tesisAId;
    private int _tesisA2Id;

    public async Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(SatisBelgesiMuhasebeTestSupport.ConnectionString))
        {
            return;
        }

        _uniqueSuffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];

        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var (kurum, il, tesis) = await SatisBelgesiMuhasebeTestSupport.SeedKurumIlTesisAsync(dbContext, _uniqueSuffix);
        _kurumAId = kurum.Id;
        _ilAId = il.Id;
        _tesisAId = tesis.Id;

        // Aynı kurum içinde İKİNCİ bir tesis - "aynı kuruma ait tesise taşıma serbest" senaryosu için.
        var tesisA2 = new STYS.Tesisler.Entities.Tesis
        {
            KurumId = kurum.Id,
            IlId = il.Id,
            Ad = "Test Tesis 2 " + _uniqueSuffix,
            Telefon = "0000",
            Adres = "Test Adres 2",
            AktifMi = true
        };
        dbContext.Tesisler.Add(tesisA2);
        await dbContext.SaveChangesAsync();
        _tesisA2Id = tesisA2.Id;
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrWhiteSpace(SatisBelgesiMuhasebeTestSupport.ConnectionString) || _kurumAId <= 0)
        {
            return;
        }

        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        await dbContext.Tesisler.Where(x => x.Id == _tesisA2Id).ExecuteDeleteAsync();
        await SatisBelgesiMuhasebeTestSupport.CleanupAsync(dbContext, _uniqueSuffix, _tesisAId, _kurumAId, _ilAId);
    }

    private static CreateSatisBelgesiRequest BuildRequest(int tesisId, string belgeNo) => new()
    {
        BelgeNo = belgeNo,
        BelgeTipi = SatisBelgesiTipi.Proforma,
        TesisId = tesisId,
        BelgeTarihi = new DateTime(2026, 1, 15),
        MusteriAdSoyad = "Test Musteri",
        Satirlar =
        [
            new CreateSatisBelgesiSatiriRequest
            {
                SiraNo = 1,
                Aciklama = "Test satir",
                Miktar = 1,
                BirimFiyat = 100m,
                KdvUygulamaTipi = (int)STYS.Muhasebe.Kdv.Enums.KdvUygulamaTipi.Kdvli,
                KdvOrani = 20m
            }
        ]
    };

    [IntegrationFact]
    public async Task CreateAsync_KurumIdOtoriterOlarakTesisUzerindenAtanir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var created = await service.CreateAsync(
            BuildRequest(_tesisAId, $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40]));

        Assert.Equal(_kurumAId, created.KurumId);

        var dbSatir = await dbContext.SatisBelgeleri.AsNoTracking().FirstAsync(x => x.Id == created.Id);
        Assert.Equal(_kurumAId, dbSatir.KurumId);
    }

    [IntegrationFact]
    public async Task SaveChanges_KurumIdDogrudanDegistirilmeyeCalisilirsa_ApplyTenantRulesReddeder()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var created = await service.CreateAsync(
            BuildRequest(_tesisAId, $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40]));

        // CreateSatisBelgesiRequest/UpdateSatisBelgesiRequest hiçbir zaman KurumId alanı
        // İÇERMEDİĞİNDEN (istemci bu alanı hiçbir servis akışıyla gönderemez), immutability'i
        // en alt seviyede - doğrudan entity/DbContext üzerinden - doğrulamak gerekir. Bu, mevcut
        // StysAppDbContext.ApplyTenantRules altyapısının SatisBelgesi için de çalıştığını kanıtlar.
        var tracked = await dbContext.SatisBelgeleri.FirstAsync(x => x.Id == created.Id);
        tracked.KurumId = _kurumAId + 999999;

        var ex = await Assert.ThrowsAsync<BaseException>(() => dbContext.SaveChangesAsync());
        Assert.Contains("KurumId", ex.Message);
    }

    [IntegrationFact]
    public async Task CreateAsync_BaskaKurumaAitTesisScopedKullaniciTarafindanSecilemez_TesisBulunamaz()
    {
        var uniqueSuffixB = $"{TestMarker}-B-{Guid.NewGuid():N}"[..24];
        int kurumBId = 0, ilBId = 0, tesisBId = 0;

        try
        {
            await using (var seedContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext())
            {
                var (kurumB, ilB, tesisB) = await SatisBelgesiMuhasebeTestSupport.SeedKurumIlTesisAsync(seedContext, uniqueSuffixB);
                kurumBId = kurumB.Id;
                ilBId = ilB.Id;
                tesisBId = tesisB.Id;
            }

            await using var scopedDbContext = CreateScopedDbContext(_kurumAId);
            var scopedService = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(scopedDbContext);

            var ex = await Assert.ThrowsAsync<BaseException>(() =>
                scopedService.CreateAsync(BuildRequest(tesisBId, $"BLG-{uniqueSuffixB}-{Guid.NewGuid():N}"[..40])));

            Assert.Equal(404, ex.ErrorCode);
        }
        finally
        {
            if (kurumBId > 0)
            {
                await using var cleanupContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
                await SatisBelgesiMuhasebeTestSupport.CleanupAsync(cleanupContext, uniqueSuffixB, tesisBId, kurumBId, ilBId);
            }
        }
    }

    [IntegrationFact]
    public async Task ScopedKullanici_KendiKurumundaBelgeOlusturupGorebilir()
    {
        await using var scopedDbContext = CreateScopedDbContext(_kurumAId);
        var scopedService = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(scopedDbContext);

        var created = await scopedService.CreateAsync(
            BuildRequest(_tesisAId, $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40]));

        Assert.Equal(_kurumAId, created.KurumId);

        var fetched = await scopedService.GetByIdAsync(created.Id!.Value);
        Assert.Equal(created.Id, fetched.Id);
    }

    [IntegrationFact]
    public async Task ScopedKullanici_BaskaKurumunBelgesiniIdIleGoremez_404()
    {
        var uniqueSuffixB = $"{TestMarker}-B-{Guid.NewGuid():N}"[..24];
        int kurumBId = 0, ilBId = 0, tesisBId = 0;

        try
        {
            int belgeBId;
            await using (var seedContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext())
            {
                var (kurumB, ilB, tesisB) = await SatisBelgesiMuhasebeTestSupport.SeedKurumIlTesisAsync(seedContext, uniqueSuffixB);
                kurumBId = kurumB.Id;
                ilBId = ilB.Id;
                tesisBId = tesisB.Id;

                var superAdminService = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(seedContext);
                var belgeB = await superAdminService.CreateAsync(
                    BuildRequest(tesisBId, $"BLG-{uniqueSuffixB}-{Guid.NewGuid():N}"[..40]));
                belgeBId = belgeB.Id!.Value;
            }

            await using var scopedDbContextA = CreateScopedDbContext(_kurumAId);
            var scopedServiceA = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(scopedDbContextA);

            var ex = await Assert.ThrowsAsync<BaseException>(() => scopedServiceA.GetByIdAsync(belgeBId));
            Assert.Equal(404, ex.ErrorCode);
        }
        finally
        {
            if (kurumBId > 0)
            {
                await using var cleanupContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
                await SatisBelgesiMuhasebeTestSupport.CleanupAsync(cleanupContext, uniqueSuffixB, tesisBId, kurumBId, ilBId);
            }
        }
    }

    [IntegrationFact]
    public async Task ScopedKullanici_FilterAsync_BaskaKurumunBelgesiniListelemez()
    {
        var uniqueSuffixB = $"{TestMarker}-B-{Guid.NewGuid():N}"[..24];
        int kurumBId = 0, ilBId = 0, tesisBId = 0;

        try
        {
            string belgeNoA, belgeNoB;
            await using (var seedContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext())
            {
                var (kurumB, ilB, tesisB) = await SatisBelgesiMuhasebeTestSupport.SeedKurumIlTesisAsync(seedContext, uniqueSuffixB);
                kurumBId = kurumB.Id;
                ilBId = ilB.Id;
                tesisBId = tesisB.Id;

                var superAdminService = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(seedContext);
                belgeNoA = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40];
                belgeNoB = $"BLG-{uniqueSuffixB}-{Guid.NewGuid():N}"[..40];
                await superAdminService.CreateAsync(BuildRequest(_tesisAId, belgeNoA));
                await superAdminService.CreateAsync(BuildRequest(tesisBId, belgeNoB));
            }

            await using var scopedDbContextA = CreateScopedDbContext(_kurumAId);
            var scopedServiceA = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(scopedDbContextA);

            var sonuc = await scopedServiceA.FilterAsync(new SatisBelgesiFilterDto());

            Assert.Contains(sonuc, x => x.BelgeNo == belgeNoA);
            Assert.DoesNotContain(sonuc, x => x.BelgeNo == belgeNoB);
        }
        finally
        {
            if (kurumBId > 0)
            {
                await using var cleanupContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
                await SatisBelgesiMuhasebeTestSupport.CleanupAsync(cleanupContext, uniqueSuffixB, tesisBId, kurumBId, ilBId);
            }
        }
    }

    [IntegrationFact]
    public async Task SuperAdmin_HerIkiKurumunBelgesiniDeGorebilir()
    {
        var uniqueSuffixB = $"{TestMarker}-B-{Guid.NewGuid():N}"[..24];
        int kurumBId = 0, ilBId = 0, tesisBId = 0;

        try
        {
            int belgeAId, belgeBId;
            await using (var seedContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext())
            {
                var (kurumB, ilB, tesisB) = await SatisBelgesiMuhasebeTestSupport.SeedKurumIlTesisAsync(seedContext, uniqueSuffixB);
                kurumBId = kurumB.Id;
                ilBId = ilB.Id;
                tesisBId = tesisB.Id;

                var superAdminService = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(seedContext);
                var belgeA = await superAdminService.CreateAsync(
                    BuildRequest(_tesisAId, $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40]));
                var belgeB = await superAdminService.CreateAsync(
                    BuildRequest(tesisBId, $"BLG-{uniqueSuffixB}-{Guid.NewGuid():N}"[..40]));
                belgeAId = belgeA.Id!.Value;
                belgeBId = belgeB.Id!.Value;
            }

            await using var superAdminDbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
            var superAdminService2 = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(superAdminDbContext);

            var fetchedA = await superAdminService2.GetByIdAsync(belgeAId);
            var fetchedB = await superAdminService2.GetByIdAsync(belgeBId);

            Assert.Equal(_kurumAId, fetchedA.KurumId);
            Assert.Equal(kurumBId, fetchedB.KurumId);
        }
        finally
        {
            if (kurumBId > 0)
            {
                await using var cleanupContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
                await SatisBelgesiMuhasebeTestSupport.CleanupAsync(cleanupContext, uniqueSuffixB, tesisBId, kurumBId, ilBId);
            }
        }
    }

    [IntegrationFact]
    public async Task UpdateAsync_AyniKurumdaBaskaTesiseTasimaSerbesttir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var created = await service.CreateAsync(
            BuildRequest(_tesisAId, $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40]));

        var updated = await service.UpdateAsync(created.Id!.Value, new UpdateSatisBelgesiRequest
        {
            TesisId = _tesisA2Id
        });

        Assert.Equal(_kurumAId, updated.KurumId);
        var dbSatir = await dbContext.SatisBelgeleri.AsNoTracking().FirstAsync(x => x.Id == created.Id);
        Assert.Equal(_tesisA2Id, dbSatir.TesisId);
        Assert.Equal(_kurumAId, dbSatir.KurumId);
    }

    [IntegrationFact]
    public async Task UpdateAsync_BaskaKurumaAitTesiseTasinamaz()
    {
        var uniqueSuffixB = $"{TestMarker}-B-{Guid.NewGuid():N}"[..24];
        int kurumBId = 0, ilBId = 0, tesisBId = 0;

        try
        {
            await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
            var (kurumB, ilB, tesisB) = await SatisBelgesiMuhasebeTestSupport.SeedKurumIlTesisAsync(dbContext, uniqueSuffixB);
            kurumBId = kurumB.Id;
            ilBId = ilB.Id;
            tesisBId = tesisB.Id;

            var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
            var created = await service.CreateAsync(
                BuildRequest(_tesisAId, $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40]));

            var ex = await Assert.ThrowsAsync<BaseException>(() =>
                service.UpdateAsync(created.Id!.Value, new UpdateSatisBelgesiRequest { TesisId = tesisBId }));

            Assert.Contains("başka bir kuruma ait tesise taşınamaz", ex.Message);

            var dbSatir = await dbContext.SatisBelgeleri.AsNoTracking().FirstAsync(x => x.Id == created.Id);
            Assert.Equal(_tesisAId, dbSatir.TesisId);
            Assert.Equal(_kurumAId, dbSatir.KurumId);
        }
        finally
        {
            if (kurumBId > 0)
            {
                await using var cleanupContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
                await SatisBelgesiMuhasebeTestSupport.CleanupAsync(cleanupContext, uniqueSuffixB, tesisBId, kurumBId, ilBId);
            }
        }
    }

    [IntegrationFact]
    public void CreateVeUpdateRequest_KurumIdVeResmiNumaraAlanlariniHicIcermez()
    {
        // KurumId/ResmiFaturaNo/FaturaKesimTarihi'nin istemciden ASLA gönderilemeyeceğinin yapısal
        // kanıtı: bu alanlar DTO'larda hiç mevcut değildir (model-binding ile dahi set edilemezler).
        var createProps = typeof(CreateSatisBelgesiRequest).GetProperties().Select(p => p.Name).ToHashSet();
        var updateProps = typeof(UpdateSatisBelgesiRequest).GetProperties().Select(p => p.Name).ToHashSet();

        foreach (var yasakliAlan in new[] { "KurumId", "ResmiFaturaNo", "FaturaKesimTarihi" })
        {
            Assert.DoesNotContain(yasakliAlan, createProps);
            Assert.DoesNotContain(yasakliAlan, updateProps);
        }
    }

    private static StysAppDbContext CreateScopedDbContext(int kurumId)
    {
        if (string.IsNullOrWhiteSpace(SatisBelgesiMuhasebeTestSupport.ConnectionString))
        {
            throw new InvalidOperationException(
                $"{IntegrationFactAttribute.ConnectionStringEnvVar} ortam degiskeni tanimli degil.");
        }

        var options = new DbContextOptionsBuilder<StysAppDbContext>()
            .UseSqlServer(SatisBelgesiMuhasebeTestSupport.ConnectionString)
            .Options;

        return new StysAppDbContext(
            options,
            new SatisBelgesiMuhasebeTestSupport.FakeCurrentUserAccessor(),
            new ScopedCurrentTenantAccessor(kurumId));
    }

    private sealed class ScopedCurrentTenantAccessor(int kurumId) : TOD.Platform.Security.Auth.Services.ICurrentTenantAccessor
    {
        public int? GetCurrentKurumId() => kurumId;
        public IReadOnlyList<int> GetAccessibleKurumIds() => [kurumId];
        public bool IsSuperAdmin() => false;
        public bool IsKurumAdmin() => false;
    }
}
