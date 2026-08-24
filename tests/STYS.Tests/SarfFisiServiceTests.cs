using System.Reflection;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using STYS.AccessScope;
using STYS.Binalar.Entities;
using STYS.Iller.Entities;
using STYS.Infrastructure.EntityFramework;
using STYS.IsletmeAlanlari.Entities;
using STYS.Kurumlar.Entities;
using STYS.Muhasebe.CariKartlar.Repositories;
using STYS.Muhasebe.Common.Services;
using STYS.Muhasebe.Depolar.Entities;
using STYS.Muhasebe.Depolar.Repositories;
using STYS.Muhasebe.Kdv.Dtos;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.Kdv.Services;
using STYS.Muhasebe.MuhasebeDonemleri.Dtos;
using STYS.Muhasebe.MuhasebeDonemleri.Entities;
using STYS.Muhasebe.MuhasebeDonemleri.Services;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;
using STYS.Muhasebe.SarfFisleri.Controllers;
using STYS.Muhasebe.SarfFisleri.Dtos;
using STYS.Muhasebe.SarfFisleri.Entities;
using STYS.Muhasebe.SarfFisleri.Mapping;
using STYS.Muhasebe.SarfFisleri.Repositories;
using STYS.Muhasebe.SarfFisleri.Services;
using STYS.Muhasebe.StokHareketleri.Controllers;
using STYS.Muhasebe.StokHareketleri.Entities;
using STYS.Muhasebe.StokHareketleri.Mapping;
using STYS.Muhasebe.StokHareketleri.Repositories;
using STYS.Muhasebe.StokHareketleri.Services;
using STYS.Muhasebe.StokMaliyetPolitikalari.Dtos;
using STYS.Muhasebe.StokMaliyetPolitikalari.Entities;
using STYS.Muhasebe.StokMaliyetPolitikalari.Services;
using STYS.Muhasebe.StokTalepleri.Controllers;
using STYS.Muhasebe.TasinirKartlari.Entities;
using STYS.Muhasebe.TasinirKartlari.Mapping;
using STYS.Muhasebe.TasinirKartlari.Repositories;
using STYS.Muhasebe.TasinirKodlari.Entities;
using STYS.Tesisler.Entities;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.Persistence.Rdbms.Paging;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Tests;

public class SarfFisiServiceTests
{
    [Fact]
    public void PermissionAyrimi_ControllerAttributelerindeTanimlidir()
    {
        AssertPermission(typeof(StokTalepleriController), "Create", StructurePermissions.StokTalepYonetimi.Create);
        AssertPermission(typeof(StokTalepleriController), "UpdateSatirlar", StructurePermissions.StokTalepYonetimi.Approve);
        AssertPermission(typeof(StokTalepleriController), "TeslimEt", StructurePermissions.StokTalepYonetimi.Deliver);
        AssertPermission(typeof(StokTalepleriController), "Iptal", StructurePermissions.StokTalepYonetimi.Cancel);
        AssertPermission(typeof(StokHareketleriController), "CreateTransfer", StructurePermissions.StokDepoCikisYonetimi.Create);
        AssertPermission(typeof(SarfFisleriController), "Create", StructurePermissions.SarfYonetimi.Create);
        AssertPermission(typeof(SarfFisleriController), "Kesinlestir", StructurePermissions.SarfYonetimi.Finalize);
        AssertPermission(typeof(SarfFisleriController), "Iptal", StructurePermissions.SarfYonetimi.Cancel);
    }

    [Fact]
    public async Task TaslakSarf_StokDegistirmez()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SeedSourceStockAsync(dbContext, miktar: 10);
        var service = CreateService(dbContext);

        var created = await service.AddAsync(new SarfFisiDto
        {
            DepoId = 10,
            SarfTarihi = new DateTime(2026, 8, 24, 9, 0, 0),
            IsletmeAlaniId = 30,
            Aciklama = "Temizlik sarfı"
        });
        await service.AddSatirAsync(created.Id!.Value, new AddSarfFisiSatirRequest
        {
            TasinirKartId = 100,
            Miktar = 3
        });

        Assert.Single(await dbContext.StokHareketleri.AsNoTracking().ToListAsync());
        Assert.Equal(StokHareketTipleri.Giris, (await dbContext.StokHareketleri.AsNoTracking().SingleAsync()).HareketTipi);
    }

    [Fact]
    public async Task Kesinlestirme_SarfStokHareketiOlusturur()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SeedSourceStockAsync(dbContext, miktar: 10);
        var service = CreateService(dbContext);

        var created = await service.AddAsync(new SarfFisiDto
        {
            DepoId = 10,
            SarfTarihi = new DateTime(2026, 8, 24, 9, 0, 0),
            IsletmeAlaniId = 30
        });
        var withLine = await service.AddSatirAsync(created.Id!.Value, new AddSarfFisiSatirRequest
        {
            TasinirKartId = 100,
            Miktar = 3
        });

        var result = await service.KesinlestirAsync(withLine.Id!.Value);

        Assert.Equal(SarfFisiDurumlari.Kesinlesti, result.Durum);
        var sarfHareket = await dbContext.StokHareketleri.AsNoTracking().SingleAsync(x => x.KaynakModul == "SarfFisiSatir");
        Assert.Equal(StokHareketTipleri.Sarf, sarfHareket.HareketTipi);
        Assert.Equal(3, sarfHareket.Miktar);
    }

    [Fact]
    public async Task YetersizStoktaTumFisRollbackOlur()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SeedSourceStockAsync(dbContext, miktar: 2);
        var service = CreateService(dbContext);

        var created = await service.AddAsync(new SarfFisiDto
        {
            DepoId = 10,
            SarfTarihi = new DateTime(2026, 8, 24, 9, 0, 0),
            IsletmeAlaniId = 30
        });
        var withLine = await service.AddSatirAsync(created.Id!.Value, new AddSarfFisiSatirRequest
        {
            TasinirKartId = 100,
            Miktar = 5
        });

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.KesinlestirAsync(withLine.Id!.Value));

        Assert.Equal(400, ex.ErrorCode);
        Assert.Equal("Depoda bu işlem için yeterli stok bulunmamaktadır.", ex.Message);
        Assert.Single(await dbContext.StokHareketleri.AsNoTracking().ToListAsync());
        var fis = await dbContext.SarfFisleri.Include(x => x.Satirlar).SingleAsync(x => x.Id == withLine.Id!.Value);
        Assert.Equal(SarfFisiDurumlari.Taslak, fis.Durum);
        Assert.All(fis.Satirlar, x => Assert.Null(x.StokHareketId));
    }

    [Fact]
    public async Task SeriKurali_Korunur()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext, takipTipi: TasinirKartTakipTipleri.Seri);
        var service = CreateService(dbContext);
        var created = await service.AddAsync(new SarfFisiDto
        {
            DepoId = 10,
            SarfTarihi = new DateTime(2026, 8, 24, 9, 0, 0),
            IsletmeAlaniId = 30
        });

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.AddSatirAsync(created.Id!.Value, new AddSarfFisiSatirRequest
        {
            TasinirKartId = 100,
            Miktar = 2,
            StokSeriId = 500
        }));

        Assert.Equal(400, ex.ErrorCode);
        Assert.Equal("Seri takipli taşınır kartlarda miktar 1 olmalıdır.", ex.Message);
    }

    [Fact]
    public async Task IkinciKesinlestirme_MukerrerHareketUretmez()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SeedSourceStockAsync(dbContext, miktar: 10);
        var service = CreateService(dbContext);
        var created = await service.AddAsync(new SarfFisiDto
        {
            DepoId = 10,
            SarfTarihi = new DateTime(2026, 8, 24, 9, 0, 0),
            IsletmeAlaniId = 30
        });
        var withLine = await service.AddSatirAsync(created.Id!.Value, new AddSarfFisiSatirRequest
        {
            TasinirKartId = 100,
            Miktar = 1
        });

        await service.KesinlestirAsync(withLine.Id!.Value);
        var ex = await Assert.ThrowsAsync<BaseException>(() => service.KesinlestirAsync(withLine.Id!.Value));

        Assert.Equal(400, ex.ErrorCode);
        Assert.Equal("Sadece taslak sarf fişleri değiştirilebilir.", ex.Message);
        Assert.Equal(2, await dbContext.StokHareketleri.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task TesisScopeDisindakiSarfFisineErisimReddedilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext, tesisId: 2);
        var service = CreateService(dbContext, DomainAccessScope.Scoped([], [1], []));

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.AddAsync(new SarfFisiDto
        {
            DepoId = 10,
            SarfTarihi = new DateTime(2026, 8, 24, 9, 0, 0)
        }));

        Assert.Equal(403, ex.ErrorCode);
        Assert.Equal("Bu tesis için yetkiniz bulunmuyor.", ex.Message);
    }

    [Fact]
    public void MigrationAssembly_AddSarfFisleriAndStockPermissionSplit_DiscoverEdilir()
    {
        var options = new DbContextOptionsBuilder<StysAppDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=StysMigrationDiscoverySarf;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        using var dbContext = new StysAppDbContext(options);
        var migrationsAssembly = dbContext.GetService<IMigrationsAssembly>();

        Assert.Contains("20260824125241_AddSarfFisleriAndStockPermissionSplit", migrationsAssembly.Migrations.Keys);
    }

    private static void AssertPermission(Type controllerType, string methodName, string expectedPermission)
    {
        var method = controllerType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException($"{controllerType.Name}.{methodName} bulunamadı.");
        var attribute = method.GetCustomAttributes(typeof(PermissionAttribute), inherit: true).OfType<PermissionAttribute>().Single();
        var field = typeof(PermissionAttribute).GetField("_permissions", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("PermissionAttribute._permissions alanı bulunamadı.");
        var permissions = (string[]?)field.GetValue(attribute) ?? [];
        Assert.Contains(expectedPermission, permissions);
    }

    private static SarfFisiService CreateService(StysAppDbContext dbContext, DomainAccessScope? scope = null)
    {
        var mapper = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<StokHareketProfile>();
            cfg.AddProfile<TasinirKartProfile>();
            cfg.AddProfile<SarfFisiProfile>();
        }, NullLoggerFactory.Instance).CreateMapper();
        var muhasebeDonemService = new FakeMuhasebeDonemService();

        var stokHareketService = new StokHareketService(
            dbContext,
            new StokHareketRepository(dbContext, mapper),
            new DepoRepository(dbContext, mapper),
            new TasinirKartRepository(dbContext, mapper),
            new CariKartRepository(dbContext, mapper),
            muhasebeDonemService,
            new FakeUserAccessScopeService(scope ?? DomainAccessScope.Scoped([], [1], [])),
            new FakeKdvUygulamaService(),
            CreatePolicyService(dbContext, muhasebeDonemService, scope),
            new StokMaliyetStrategyResolver([new AgirlikliOrtalamaMaliyetStrategy(dbContext), new FifoMaliyetStrategy(dbContext), new LifoMaliyetStrategy(dbContext)]),
            mapper);

        return new SarfFisiService(
            dbContext,
            new SarfFisiRepository(dbContext, mapper),
            new DepoRepository(dbContext, mapper),
            new TasinirKartRepository(dbContext, mapper),
            new FakeUserAccessScopeService(scope ?? DomainAccessScope.Scoped([], [1], [])),
            new FakeCurrentUserAccessor(),
            stokHareketService,
            mapper);
    }

    private static IStokMaliyetPolitikasiService CreatePolicyService(StysAppDbContext dbContext, IMuhasebeDonemService muhasebeDonemService, DomainAccessScope? scope)
        => new StokMaliyetPolitikasiService(
            dbContext,
            muhasebeDonemService,
            new FakeMuhasebeTesisScopeService(),
            new FakeUserAccessScopeService(scope ?? DomainAccessScope.Scoped([], [1], [])),
            new StokHareketRepository(dbContext, new MapperConfiguration(cfg => cfg.AddProfile<StokHareketProfile>(), NullLoggerFactory.Instance).CreateMapper()));

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

    private static async Task SeedBaseAsync(StysAppDbContext dbContext, string takipTipi = TasinirKartTakipTipleri.Yok, int tesisId = 1)
    {
        dbContext.Kurumlar.Add(new Kurum { Id = 1, Kod = "TRT", Ad = "TRT", AktifMi = true });
        dbContext.Iller.Add(new Il { Id = 1, Ad = "Ankara", AktifMi = true });
        dbContext.Tesisler.Add(new Tesis
        {
            Id = tesisId,
            KurumId = 1,
            IlId = 1,
            Ad = "Tesis",
            Telefon = "000",
            Adres = "Adres",
            AktifMi = true
        });
        dbContext.Binalar.Add(new Bina
        {
            Id = 20,
            TesisId = tesisId,
            Ad = "Ana Bina",
            KatSayisi = 1,
            AktifMi = true
        });
        dbContext.IsletmeAlaniSiniflari.Add(new IsletmeAlaniSinifi
        {
            Id = 25,
            Kod = "TEMIZLIK",
            Ad = "Temizlik",
            AktifMi = true
        });
        dbContext.IsletmeAlanlari.Add(new IsletmeAlani
        {
            Id = 30,
            BinaId = 20,
            IsletmeAlaniSinifiId = 25,
            OzelAd = "Temizlik Birimi",
            AktifMi = true
        });
        dbContext.Depolar.Add(new Depo
        {
            Id = 10,
            TesisId = tesisId,
            Kod = "TEM",
            Ad = "Temizlik Depo",
            AktifMi = true,
            MuhasebeHesapPlaniId = 1,
            MalzemeKayitTipi = DepoMalzemeKayitTipleri.MalzemeleriAyriKayittaTut
        });
        dbContext.MuhasebeDonemler.Add(new MuhasebeDonem
        {
            Id = 1,
            TesisId = tesisId,
            MaliYil = 2026,
            DonemNo = 8,
            BaslangicTarihi = new DateTime(2026, 8, 1),
            BitisTarihi = new DateTime(2026, 8, 31),
            KapaliMi = false
        });
        dbContext.MuhasebeHesapPlanlari.Add(new MuhasebeHesapPlani
        {
            Id = 1,
            Kod = "150",
            TamKod = "150.01",
            Ad = "Stok",
            SeviyeNo = 2,
            HesapTipi = HesapTipi.DetayHesap,
            AktifMi = true,
            DetayHesapMi = true,
            HareketGorebilirMi = true,
            TesisId = tesisId
        });
        dbContext.TasinirKodlar.Add(new TasinirKod
        {
            Id = 1000,
            Kod = "150.01.001",
            Ad = "Temizlik",
            AktifMi = true
        });
        dbContext.TasinirKartlar.Add(new TasinirKart
        {
            Id = 100,
            TesisId = tesisId,
            TasinirKodId = 1000,
            MuhasebeHesapPlaniId = 1,
            StokKodu = "STK-100",
            Ad = "Deterjan",
            Birim = "Adet",
            MalzemeTipi = MalzemeTipleri.Diger,
            TakipliMi = takipTipi != TasinirKartTakipTipleri.Yok,
            TakipTipi = takipTipi,
            AktifMi = true,
            KdvOrani = 0
        });
        if (takipTipi == TasinirKartTakipTipleri.Seri)
        {
            dbContext.StokSeriler.Add(new STYS.Muhasebe.StokSerileri.Entities.StokSeri
            {
                Id = 500,
                TesisId = tesisId,
                TasinirKartId = 100,
                SeriNo = "SN001",
                AktifMi = true
            });
        }

        dbContext.StokMaliyetPolitikalari.Add(new StokMaliyetPolitikasi
        {
            Id = 1,
            TesisId = tesisId,
            MaliYil = 2026,
            MaliyetYontemi = StokMaliyetYontemleri.AgirlikliOrtalama
        });

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedSourceStockAsync(StysAppDbContext dbContext, decimal miktar)
    {
        dbContext.StokHareketleri.Add(new StokHareket
        {
            Id = 1,
            DepoId = 10,
            TasinirKartId = 100,
            HareketTarihi = new DateTime(2026, 8, 23, 8, 0, 0),
            HareketTipi = StokHareketTipleri.Giris,
            Miktar = miktar,
            BirimFiyat = 1,
            Tutar = miktar,
            Durum = StokHareketDurumlari.Aktif,
            KdvUygulamaTipi = (int)KdvUygulamaTipi.KdvKapsamDisi,
            KdvOrani = 0,
            KdvTutari = 0
        });

        await dbContext.SaveChangesAsync();
    }

    private sealed class FakeUserAccessScopeService : IUserAccessScopeService
    {
        private readonly DomainAccessScope _scope;
        public FakeUserAccessScopeService(DomainAccessScope scope) => _scope = scope;
        public Task<DomainAccessScope> GetCurrentScopeAsync(CancellationToken cancellationToken = default) => Task.FromResult(_scope);
    }

    private sealed class FakeCurrentUserAccessor : ICurrentUserAccessor
    {
        public Guid? GetCurrentUserId() => Guid.Parse("11111111-1111-1111-1111-111111111111");
        public string? GetCurrentUserName() => "test-user";
    }

    private sealed class FakeCurrentTenantAccessor : ICurrentTenantAccessor
    {
        public int? GetCurrentKurumId() => 1;
        public IReadOnlyList<int> GetAccessibleKurumIds() => [1];
        public bool IsSuperAdmin() => false;
        public bool IsKurumAdmin() => false;
    }

    private sealed class FakeMuhasebeTesisScopeService : IMuhasebeTesisScopeService
    {
        public Task EnsureCanAccessTesisAsync(int tesisId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<int[]> GetEffectiveTesisIdsAsync(CancellationToken cancellationToken = default) => Task.FromResult(new[] { 1 });
        public Task<int[]> GetEffectiveTesisIdsAsync(DomainAccessScope scope, CancellationToken cancellationToken = default) => Task.FromResult(new[] { 1 });
    }

    private sealed class FakeMuhasebeDonemService : IMuhasebeDonemService
    {
        public Task<MuhasebeDonemDto?> GetAktifDonemAsync(int tesisId, DateTime tarih, CancellationToken cancellationToken = default)
            => Task.FromResult<MuhasebeDonemDto?>(new MuhasebeDonemDto
            {
                Id = 1,
                TesisId = tesisId,
                BaslangicTarihi = new DateTime(2026, 8, 1),
                BitisTarihi = new DateTime(2026, 8, 31),
                KapaliMi = false,
                MaliYil = 2026,
                DonemNo = 8
            });

        public Task<MuhasebeDonemDto?> GetDonemByTarihAsync(int tesisId, DateTime tarih, CancellationToken cancellationToken = default)
            => GetAktifDonemAsync(tesisId, tarih, cancellationToken);

        public Task DonemKapatAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DonemAcAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<MuhasebeDonemDto>> GetAllAsync(Func<IQueryable<MuhasebeDonem>, IQueryable<MuhasebeDonem>>? include = null) => throw new NotSupportedException();
        public Task<MuhasebeDonemDto?> GetByIdAsync(int id, Func<IQueryable<MuhasebeDonem>, IQueryable<MuhasebeDonem>>? include = null) => throw new NotSupportedException();
        public Task<PagedResult<MuhasebeDonemDto>> GetPagedAsync(PagedRequest request, System.Linq.Expressions.Expression<Func<MuhasebeDonem, bool>>? predicate = null, Func<IQueryable<MuhasebeDonem>, IQueryable<MuhasebeDonem>>? include = null, Func<IQueryable<MuhasebeDonem>, IOrderedQueryable<MuhasebeDonem>>? orderBy = null) => throw new NotSupportedException();
        public Task<MuhasebeDonemDto> AddAsync(MuhasebeDonemDto dto) => throw new NotSupportedException();
        public Task<MuhasebeDonemDto> UpdateAsync(MuhasebeDonemDto dto) => throw new NotSupportedException();
        public Task DeleteAsync(int id) => throw new NotSupportedException();
        public Task<IEnumerable<MuhasebeDonemDto>> WhereAsync(System.Linq.Expressions.Expression<Func<MuhasebeDonem, bool>> predicate, Func<IQueryable<MuhasebeDonem>, IQueryable<MuhasebeDonem>>? include = null) => throw new NotSupportedException();
        public Task<bool> AnyAsync(System.Linq.Expressions.Expression<Func<MuhasebeDonem, bool>> predicate, Func<IQueryable<MuhasebeDonem>, IQueryable<MuhasebeDonem>>? include = null) => throw new NotSupportedException();
    }

    private sealed class FakeKdvUygulamaService : IKdvUygulamaService
    {
        public Task<KdvUygulamaResult> ValidateAndSnapshotAsync(int kdvUygulamaTipi, int? kdvIstisnaTanimId, decimal kdvOrani, decimal tutar, DateTime islemTarihi, KdvIslemYonu islemYonu, CancellationToken cancellationToken = default)
            => Task.FromResult(new KdvUygulamaResult
            {
                KdvUygulamaTipi = kdvUygulamaTipi,
                KdvIstisnaTanimId = kdvIstisnaTanimId,
                KdvOrani = kdvOrani,
                KdvTutari = 0
            });
    }
}
