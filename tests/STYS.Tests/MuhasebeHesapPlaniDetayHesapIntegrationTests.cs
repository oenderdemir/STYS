using System.Collections.Concurrent;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.Common.Services;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Dtos;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Mapping;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Repositories;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Services;
using STYS.Tesisler.Entities;
using STYS.Tests.TestSupport;
using TOD.Platform.SharedKernel.Exceptions;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// "Detay Hesap Ekle" akışı: ana hesap altında, çalışma tesisi kapsamında güvenli detay hesap
/// oluşturma — merkezi MuhasebeDetayHesapService yeniden kullanılır; tesis request body'den alınmaz.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SqlServerIntegrationCollection.Name)]
public class MuhasebeHesapPlaniDetayHesapIntegrationTests : IAsyncLifetime
{
    private const string TestMarker = "MHPDET-778";

    private string _uniqueSuffix = TestMarker;
    private int _kurumId;
    private int _ilId;
    private int _tesisId;
    private int _ankaraTesisId;
    private int _digerKurumId;
    private int _digerIlId;
    private int _digerTesisId;
    private int _anaHesapId;

    public async Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(SatisBelgesiMuhasebeTestSupport.ConnectionString))
        {
            return;
        }

        _uniqueSuffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];

        await using var db = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var (kurum, il, tesis) = await SatisBelgesiMuhasebeTestSupport.SeedKurumIlTesisAsync(db, _uniqueSuffix);
        _kurumId = kurum.Id;
        _ilId = il.Id;
        _tesisId = tesis.Id;

        var ankaraTesis = new Tesis
        {
            KurumId = _kurumId,
            IlId = _ilId,
            Ad = "Test Ankara Tesis " + _uniqueSuffix,
            Telefon = "0000",
            Adres = "Test Ankara Adres",
            AktifMi = true
        };
        db.Tesisler.Add(ankaraTesis);

        var (digerKurum, digerIl, digerTesis) = await SatisBelgesiMuhasebeTestSupport.SeedKurumIlTesisAsync(db, _uniqueSuffix + "-DIGER");
        _digerKurumId = digerKurum.Id;
        _digerIlId = digerIl.Id;
        _digerTesisId = digerTesis.Id;

        await db.SaveChangesAsync();
        _ankaraTesisId = ankaraTesis.Id;

        // Global ana hesap (6.60.600): Aktif, detay DEĞİL, hareket görebilir DEĞİL.
        var anaHesap = new MuhasebeHesapPlani
        {
            Kod = $"6.60.600.{_uniqueSuffix}",
            TamKod = $"6.60.600.{_uniqueSuffix}",
            Ad = "YURT İÇİ SATIŞLAR",
            SeviyeNo = 3,
            HesapTipi = HesapTipi.AnaHesap,
            KurumId = null,
            TesisId = null,
            AktifMi = true,
            DetayHesapMi = false,
            HareketGorebilirMi = false
        };
        db.MuhasebeHesapPlanlari.Add(anaHesap);
        await db.SaveChangesAsync();
        _anaHesapId = anaHesap.Id;
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrWhiteSpace(SatisBelgesiMuhasebeTestSupport.ConnectionString) || _kurumId <= 0)
        {
            return;
        }

        await using var db = SatisBelgesiMuhasebeTestSupport.CreateDbContext();

        var tesisIds = new[] { _tesisId, _ankaraTesisId, _digerTesisId }.Where(x => x > 0).ToArray();

        // MuhasebeDetayHesapService, tesis bazlı MuhasebeHesapKoduSayaclari kaydı oluşturur ve bu
        // tablo Tesisler'e Restrict FK ile bağlıdır — tesisler silinmeden önce temizlenir.
        await db.Set<MuhasebeHesapKoduSayac>()
            .IgnoreQueryFilters()
            .Where(x => tesisIds.Contains(x.TesisId))
            .ExecuteDeleteAsync();

        await db.MuhasebeHesapPlanlari
            .IgnoreQueryFilters()
            .Where(x => x.Kod != null && x.Kod.Contains(_uniqueSuffix))
            .ExecuteDeleteAsync();

        if (_ankaraTesisId > 0)
        {
            await db.Tesisler.Where(x => x.Id == _ankaraTesisId).ExecuteDeleteAsync();
        }

        await SatisBelgesiMuhasebeTestSupport.CleanupAsync(db, _uniqueSuffix, _tesisId, _kurumId, _ilId);

        if (_digerKurumId > 0)
        {
            await SatisBelgesiMuhasebeTestSupport.CleanupAsync(db, _uniqueSuffix + "-DIGER", _digerTesisId, _digerKurumId, _digerIlId);
        }
    }

    private static IMapper CreateMapper()
    {
        var config = new MapperConfiguration(cfg => cfg.AddProfile<MuhasebeHesapPlaniProfile>(), NullLoggerFactory.Instance);
        return config.CreateMapper();
    }

    private static IMuhasebeHesapPlaniService CreateService(
        StysAppDbContext db, int[] effectiveTesisIds, int? accessibleTesisId = null)
    {
        var mapper = CreateMapper();
        var repository = new MuhasebeHesapPlaniRepository(db, mapper);
        var detayHesapService = new MuhasebeDetayHesapService(db);

        return new MuhasebeHesapPlaniService(
            repository,
            mapper,
            new FakeDistributedCache(),
            db,
            new FakeTesisScopeService(effectiveTesisIds, accessibleTesisId),
            new SatisBelgesiMuhasebeTestSupport.FakeCurrentTenantAccessor(),
            detayHesapService);
    }

    [IntegrationFact]
    public async Task DetayHesapOlustur_GlobalAnaHesapAltinda_DogruAlanlarlaOlusturur()
    {
        await using var db = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = CreateService(db, [_tesisId]);

        var dto = await service.CreateDetayHesapAsync(_anaHesapId, "TRT Trabzon Misafirhanesi Konaklama Gelirleri", _tesisId);

        Assert.NotNull(dto.Id);
        Assert.Equal(_kurumId, dto.KurumId);
        Assert.Equal(_tesisId, dto.TesisId);
        Assert.Equal(_anaHesapId, dto.UstHesapId);
        Assert.StartsWith("6.60.600.", dto.TamKod);
        Assert.Equal(4, dto.SeviyeNo); // ana.SeviyeNo (3) + 1
        Assert.True(dto.AktifMi);
        Assert.True(dto.DetayHesapMi);
        Assert.True(dto.HareketGorebilirMi);
    }

    [IntegrationFact]
    public async Task DetayHesapOlustur_ClientBaskaTesisSpoofEder_TesisScopeReddeder()
    {
        await using var db = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        // effective tesis: [_tesisId]; client başka bir tesis (999999) gönderirse EnsureCanAccessTesisAsync 403 verir.
        var service = CreateService(db, [_tesisId]);

        var ex = await Assert.ThrowsAsync<BaseException>(
            () => service.CreateDetayHesapAsync(_anaHesapId, "Spoof", 999999));
        Assert.Equal(403, ex.ErrorCode);
    }

    [IntegrationFact]
    public async Task TreeRoots_YetkisizTesisIdQueryParam_ForbiddenDoner()
    {
        await using var db = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = CreateService(db, [_tesisId], accessibleTesisId: _tesisId);

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.GetTreeRootsAsync(_ankaraTesisId));
        Assert.Equal(403, ex.ErrorCode);
    }

    [IntegrationFact]
    public async Task TreeRoots_SeciliTesisIcin_GlobalKurumVeSeciliTesisGorunur_DigerKapsamlarGorunmez()
    {
        await using var db = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        db.MuhasebeHesapPlanlari.AddRange(
            BuildRoot("7.10.001", "Global", null, null),
            BuildRoot("7.10.002", "Kurum", _kurumId, null),
            BuildRoot("7.10.003", "Secili Tesis", _kurumId, _tesisId),
            BuildRoot("7.10.004", "Ayni Kurum Diger Tesis", _kurumId, _ankaraTesisId),
            BuildRoot("7.10.005", "Diger Kurum", _digerKurumId, null),
            BuildRoot("7.10.006", "Diger Kurum Tesis", _digerKurumId, _digerTesisId));
        await db.SaveChangesAsync();

        var service = CreateService(db, [_tesisId, _ankaraTesisId]);

        var roots = await service.GetTreeRootsAsync(_tesisId);
        var tamKodlar = roots.Select(x => x.TamKod).ToHashSet();

        Assert.Contains($"7.10.001.{_uniqueSuffix}", tamKodlar);
        Assert.Contains($"7.10.002.{_uniqueSuffix}", tamKodlar);
        Assert.Contains($"7.10.003.{_uniqueSuffix}", tamKodlar);
        Assert.DoesNotContain($"7.10.004.{_uniqueSuffix}", tamKodlar);
        Assert.DoesNotContain($"7.10.005.{_uniqueSuffix}", tamKodlar);
        Assert.DoesNotContain($"7.10.006.{_uniqueSuffix}", tamKodlar);
    }

    [IntegrationFact]
    public async Task TreeRoots_HasChildren_SeciliTesisKapsamiDisindakiCocuklaTrueOlmaz()
    {
        await using var db = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var parent = BuildRoot("7.20.001", "Global Parent", null, null);
        db.MuhasebeHesapPlanlari.Add(parent);
        await db.SaveChangesAsync();

        db.MuhasebeHesapPlanlari.Add(new MuhasebeHesapPlani
        {
            Kod = $"7.20.001.001.{_uniqueSuffix}",
            TamKod = $"7.20.001.001.{_uniqueSuffix}",
            Ad = "Başka tesis çocuğu",
            SeviyeNo = 2,
            HesapTipi = HesapTipi.AnaHesap,
            KurumId = _kurumId,
            TesisId = _ankaraTesisId,
            UstHesapId = parent.Id,
            AktifMi = true,
            DetayHesapMi = false,
            HareketGorebilirMi = false
        });
        await db.SaveChangesAsync();

        var service = CreateService(db, [_tesisId, _ankaraTesisId]);

        var roots = await service.GetTreeRootsAsync(_tesisId);
        var scopedParent = Assert.Single(roots.Where(x => x.TamKod == $"7.20.001.{_uniqueSuffix}"));
        Assert.False(scopedParent.HasChildren);
    }

    [IntegrationFact]
    public async Task TreeRoots_CalismaTesisiYoksa_BadRequestDoner()
    {
        await using var db = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = CreateService(db, [_tesisId]);

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.GetTreeRootsAsync((int?)null));
        Assert.Equal(400, ex.ErrorCode);
    }

    private MuhasebeHesapPlani BuildRoot(string kodPrefix, string ad, int? kurumId, int? tesisId) => new()
    {
        Kod = $"{kodPrefix}.{_uniqueSuffix}",
        TamKod = $"{kodPrefix}.{_uniqueSuffix}",
        Ad = ad,
        SeviyeNo = 1,
        HesapTipi = HesapTipi.AnaHesap,
        KurumId = kurumId,
        TesisId = tesisId,
        AktifMi = true,
        DetayHesapMi = false,
        HareketGorebilirMi = false
    };

    private sealed class FakeTesisScopeService(int[] effectiveTesisIds, int? accessibleTesisId) : IMuhasebeTesisScopeService
    {
        public Task<int[]> GetEffectiveTesisIdsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(effectiveTesisIds);

        public Task<int[]> GetEffectiveTesisIdsAsync(DomainAccessScope scope, CancellationToken cancellationToken = default)
            => Task.FromResult(effectiveTesisIds);

        public Task EnsureCanAccessTesisAsync(int tesisId, CancellationToken cancellationToken = default)
        {
            var allowed = accessibleTesisId.HasValue
                ? tesisId == accessibleTesisId.Value
                : effectiveTesisIds.Contains(tesisId);
            if (!allowed)
            {
                throw new BaseException("Seçilen tesis için yetkiniz bulunmuyor.", 403);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FakeDistributedCache : IDistributedCache
    {
        private readonly ConcurrentDictionary<string, byte[]> _store = new();

        public byte[]? Get(string key) => _store.TryGetValue(key, out var v) ? v : null;
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromResult(Get(key));
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => _store[key] = value;
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) { Set(key, value, options); return Task.CompletedTask; }
        public void Refresh(string key) { }
        public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;
        public void Remove(string key) => _store.TryRemove(key, out _);
        public Task RemoveAsync(string key, CancellationToken token = default) { Remove(key); return Task.CompletedTask; }
    }
}
