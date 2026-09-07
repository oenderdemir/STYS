using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using STYS.AccessScope;
using STYS.Iller.Entities;
using STYS.Infrastructure.EntityFramework;
using STYS.Kurumlar.Entities;
using STYS.Muhasebe.CariHareketler.Dtos;
using STYS.Muhasebe.CariHareketler.Entities;
using STYS.Muhasebe.CariHareketler.Mapping;
using STYS.Muhasebe.CariHareketler.Repositories;
using STYS.Muhasebe.CariHareketler.Services;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.CariKartlar.Mapping;
using STYS.Muhasebe.CariKartlar.Repositories;
using STYS.Muhasebe.Common.Services;
using STYS.Muhasebe.MuhasebeDonemleri.Mapping;
using STYS.Muhasebe.MuhasebeDonemleri.Repositories;
using STYS.Muhasebe.MuhasebeDonemleri.Services;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;
using STYS.Tesisler.Entities;
using TOD.Platform.Persistence.Rdbms.Paging;
using TOD.Platform.Security.Auth.Services;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// Cari Hareketler ekranının server-side filtre + cari gösterim alanları davranışı.
/// (Tahsilat/Odeme Belgeleri ekranındaki aynı pattern'in cari hareketlere uygulanması.)
/// </summary>
public class CariHareketlerFilterTests
{
    [Fact]
    public async Task GetPagedWithFilter_CariDisplayAlanlariniDondurur()
    {
        await using var db = CreateDbContext();
        await SeedBaseAsync(db);
        await SeedHareketAsync(db, 500, 101, "THS-A", new DateTime(2026, 8, 24, 10, 0, 0));

        var service = CreateService(db);
        var result = await service.GetPagedWithFilterAsync(new CariHareketFilterRequest { TesisId = 1 }, new PagedRequest());

        var item = result.Items.Single();
        Assert.Equal("CK-TURAN", item.CariKodu);
        Assert.Equal("TURAN YAYLA", item.CariUnvanAdSoyad);
        Assert.Equal("11111111111", item.CariVergiNoTckn);
    }

    [Fact]
    public async Task GetPagedWithFilter_CariArama_IsimKodVeTcknIleBulur()
    {
        await using var db = CreateDbContext();
        await SeedBaseAsync(db);
        await SeedHareketAsync(db, 500, 101, "THS-T", new DateTime(2026, 8, 24, 10, 0, 0));
        await SeedHareketAsync(db, 501, 102, "THS-I", new DateTime(2026, 8, 24, 10, 0, 0));

        var service = CreateService(db);

        var byIsim = await service.GetPagedWithFilterAsync(new CariHareketFilterRequest { TesisId = 1, CariArama = "TURAN" }, new PagedRequest());
        Assert.Single(byIsim.Items);
        Assert.Equal("THS-T", byIsim.Items[0].BelgeNo);

        var byKod = await service.GetPagedWithFilterAsync(new CariHareketFilterRequest { TesisId = 1, CariArama = "CK-ILIMDAR" }, new PagedRequest());
        Assert.Single(byKod.Items);
        Assert.Equal("THS-I", byKod.Items[0].BelgeNo);

        var byTckn = await service.GetPagedWithFilterAsync(new CariHareketFilterRequest { TesisId = 1, CariArama = "22222222222" }, new PagedRequest());
        Assert.Single(byTckn.Items);
        Assert.Equal("THS-I", byTckn.Items[0].BelgeNo);
    }

    [Fact]
    public async Task GetPagedWithFilter_TarihVeKapamaDurumu_DogruCalisir()
    {
        await using var db = CreateDbContext();
        await SeedBaseAsync(db);
        await SeedHareketAsync(db, 500, 101, "ACIK", new DateTime(2026, 8, 1, 10, 0, 0), kapandiMi: false, kapananTutar: 0m);
        await SeedHareketAsync(db, 501, 101, "KISMI", new DateTime(2026, 8, 10, 10, 0, 0), kapandiMi: false, kapananTutar: 10m);
        await SeedHareketAsync(db, 502, 101, "KAPALI", new DateTime(2026, 8, 20, 10, 0, 0), kapandiMi: true, kapananTutar: 50m);

        var service = CreateService(db);

        var kapali = await service.GetPagedWithFilterAsync(new CariHareketFilterRequest { TesisId = 1, KapamaDurumu = "Kapali" }, new PagedRequest());
        Assert.Single(kapali.Items);
        Assert.Equal("KAPALI", kapali.Items[0].BelgeNo);

        var acik = await service.GetPagedWithFilterAsync(new CariHareketFilterRequest { TesisId = 1, KapamaDurumu = "Acik" }, new PagedRequest());
        Assert.Single(acik.Items);
        Assert.Equal("ACIK", acik.Items[0].BelgeNo);

        var kismi = await service.GetPagedWithFilterAsync(new CariHareketFilterRequest { TesisId = 1, KapamaDurumu = "Kismi" }, new PagedRequest());
        Assert.Single(kismi.Items);
        Assert.Equal("KISMI", kismi.Items[0].BelgeNo);

        // Tarih aralığı (dahil bitiş): 8-15 Ağustos arası → KISMI (10 Ağustos) hariç.
        var tarih = await service.GetPagedWithFilterAsync(
            new CariHareketFilterRequest { TesisId = 1, BaslangicTarihi = new DateTime(2026, 8, 5), BitisTarihi = new DateTime(2026, 8, 15) },
            new PagedRequest());
        Assert.Single(tarih.Items);
        Assert.Equal("KISMI", tarih.Items[0].BelgeNo);
    }

    [Fact]
    public async Task GetPagedWithFilter_TesisVeCariArama_BaskaTesisSizmaz()
    {
        await using var db = CreateDbContext();
        await SeedBaseAsync(db);
        await SeedHareketAsync(db, 500, 101, "THS-A", new DateTime(2026, 8, 24, 10, 0, 0)); // tesis 1, TURAN YAYLA
        await SeedHareketAsync(db, 501, 103, "THS-B", new DateTime(2026, 8, 24, 10, 0, 0)); // tesis 2, TURAN BASKA

        var service = CreateService(db);
        var result = await service.GetPagedWithFilterAsync(new CariHareketFilterRequest { TesisId = 1, CariArama = "TURAN" }, new PagedRequest());

        Assert.Single(result.Items);
        Assert.Equal("THS-A", result.Items[0].BelgeNo);
    }

    private static async Task SeedBaseAsync(StysAppDbContext db)
    {
        db.Iller.Add(new Il { Id = 1, Ad = "Ankara", AktifMi = true });
        db.Kurumlar.Add(new Kurum { Id = 1, Kod = "KRM", Ad = "Test Kurum", AktifMi = true });
        db.Tesisler.Add(new Tesis { Id = 1, KurumId = 1, IlId = 1, Ad = "Tesis A", Telefon = "03120000000", Adres = "Adres A", AktifMi = true });
        db.Tesisler.Add(new Tesis { Id = 2, KurumId = 1, IlId = 1, Ad = "Tesis B", Telefon = "03120000000", Adres = "Adres B", AktifMi = true });
        db.MuhasebeHesapPlanlari.Add(new MuhasebeHesapPlani { Id = 1, Kod = "120.01", TamKod = "120.01", Ad = "Cari Hesap", AktifMi = true, DetayHesapMi = true, HareketGorebilirMi = true, HesapTipi = HesapTipi.DetayHesap });
        db.CariKartlar.AddRange(
            new CariKart { Id = 100, TesisId = 1, CariTipi = CariKartTipleri.Musteri, CariKodu = "CR-001", UnvanAdSoyad = "Cari Kart", AktifMi = true, MuhasebeHesapPlaniId = 1 },
            new CariKart { Id = 101, TesisId = 1, CariTipi = CariKartTipleri.Musteri, CariKodu = "CK-TURAN", UnvanAdSoyad = "TURAN YAYLA", VergiNoTckn = "11111111111", AktifMi = true, MuhasebeHesapPlaniId = 1 },
            new CariKart { Id = 102, TesisId = 1, CariTipi = CariKartTipleri.Musteri, CariKodu = "CK-ILIMDAR", UnvanAdSoyad = "İLİMDAR KARAKOÇ", VergiNoTckn = "22222222222", AktifMi = true, MuhasebeHesapPlaniId = 1 },
            new CariKart { Id = 103, TesisId = 2, CariTipi = CariKartTipleri.Musteri, CariKodu = "CK-BASKA", UnvanAdSoyad = "TURAN BASKA", VergiNoTckn = "33333333333", AktifMi = true, MuhasebeHesapPlaniId = 1 });
        await db.SaveChangesAsync();
    }

    private static async Task SeedHareketAsync(
        StysAppDbContext db, int id, int cariKartId, string belgeNo, DateTime tarih,
        bool kapandiMi = false, decimal kapananTutar = 0m)
    {
        db.CariHareketler.Add(new CariHareket
        {
            Id = id,
            CariKartId = cariKartId,
            HareketTarihi = tarih,
            BelgeTuru = "Tahsilat",
            BelgeNo = belgeNo,
            Aciklama = "test",
            BorcTutari = 100m,
            AlacakTutari = 0m,
            KapananTutar = kapananTutar,
            KalanTutar = 100m - kapananTutar,
            ParaBirimi = "TRY",
            Durum = CariHareketDurumlari.Aktif,
            KapandiMi = kapandiMi
        });
        await db.SaveChangesAsync();
    }

    private static StysAppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<StysAppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new StysAppDbContext(options, new FakeCurrentUserAccessor(), new FakeCurrentTenantAccessor())
        {
            AllowExplicitTenantWritesWithoutAmbientTenant = true
        };
    }

    private static IMapper CreateMapper()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<CariHareketProfile>();
            cfg.AddProfile<CariKartProfile>();
            cfg.AddProfile<MuhasebeDonemProfile>();
        }, NullLoggerFactory.Instance);

        return config.CreateMapper();
    }

    private static ICariHareketService CreateService(StysAppDbContext db)
    {
        var mapper = CreateMapper();
        var cariHareketRepo = new CariHareketRepository(db, mapper);
        var cariKartRepo = new CariKartRepository(db, mapper);
        var donemRepo = new MuhasebeDonemRepository(db, mapper);
        var donemService = new MuhasebeDonemService(donemRepo, mapper, db, new FakeMuhasebeTesisScopeService());

        return new CariHareketService(cariHareketRepo, cariKartRepo, donemService, new FakeUserAccessScopeService(), mapper);
    }

    private sealed class FakeCurrentUserAccessor : ICurrentUserAccessor
    {
        public string? GetCurrentUserName() => "cari-hareket-test";
        public Guid? GetCurrentUserId() => Guid.Parse("33333333-3333-3333-3333-333333333333");
    }

    private sealed class FakeCurrentTenantAccessor : ICurrentTenantAccessor
    {
        public int? GetCurrentKurumId() => null;
        public IReadOnlyList<int> GetAccessibleKurumIds() => [];
        public bool IsSuperAdmin() => true;
        public bool IsKurumAdmin() => false;
    }

    private sealed class FakeUserAccessScopeService : IUserAccessScopeService
    {
        public Task<DomainAccessScope> GetCurrentScopeAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(DomainAccessScope.Unscoped());
    }

    private sealed class FakeMuhasebeTesisScopeService : IMuhasebeTesisScopeService
    {
        public Task<int[]> GetEffectiveTesisIdsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Array.Empty<int>());

        public Task<int[]> GetEffectiveTesisIdsAsync(DomainAccessScope scope, CancellationToken cancellationToken = default)
            => Task.FromResult(Array.Empty<int>());

        public Task EnsureCanAccessTesisAsync(int tesisId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
