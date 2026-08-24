using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using STYS.AccessScope;
using STYS.Iller.Entities;
using STYS.Infrastructure.EntityFramework;
using STYS.Kurumlar.Entities;
using STYS.Muhasebe.CariKartlar.Repositories;
using STYS.Muhasebe.Common.Services;
using STYS.Muhasebe.Depolar.Entities;
using STYS.Muhasebe.Depolar.Repositories;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.Kdv.Services;
using STYS.Muhasebe.MuhasebeDonemleri.Entities;
using STYS.Muhasebe.MuhasebeDonemleri.Dtos;
using STYS.Muhasebe.MuhasebeDonemleri.Services;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;
using STYS.Muhasebe.StokHareketleri.Entities;
using STYS.Muhasebe.StokHareketleri.Dtos;
using STYS.Muhasebe.StokHareketleri.Mapping;
using STYS.Muhasebe.StokHareketleri.Repositories;
using STYS.Muhasebe.StokHareketleri.Services;
using STYS.Muhasebe.StokMaliyetPolitikalari.Entities;
using STYS.Muhasebe.StokMaliyetPolitikalari.Dtos;
using STYS.Muhasebe.StokMaliyetPolitikalari.Services;
using STYS.Muhasebe.StokTalepleri.Dtos;
using STYS.Muhasebe.StokTalepleri.Entities;
using STYS.Muhasebe.StokTalepleri.Mapping;
using STYS.Muhasebe.StokTalepleri.Repositories;
using STYS.Muhasebe.StokTalepleri.Services;
using STYS.Muhasebe.TasinirKartlari.Entities;
using STYS.Muhasebe.TasinirKartlari.Mapping;
using STYS.Muhasebe.TasinirKartlari.Repositories;
using STYS.Muhasebe.TasinirKodlari.Entities;
using STYS.Tesisler.Entities;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;
using TOD.Platform.Persistence.Rdbms.Paging;

namespace STYS.Tests;

public class StokTalepServiceTests
{
    [Fact]
    public async Task GonderAsync_StokHareketiOlusturmaz()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var service = CreateService(dbContext);

        var created = await service.AddAsync(new StokTalepDto
        {
            TalepEdenDepoId = 20,
            KarsilayanDepoId = 10,
            TalepTarihi = new DateTime(2026, 8, 23, 10, 0, 0),
            Aciklama = "Temizlik ihtiyaci"
        });
        await service.AddSatirAsync(created.Id!.Value, new AddStokTalepSatirRequest
        {
            TasinirKartId = 100,
            TalepMiktari = 50
        });

        await service.GonderAsync(created.Id.Value);

        Assert.Empty(await dbContext.StokHareketleri.ToListAsync());
        var talep = await dbContext.StokTalepler.AsNoTracking().SingleAsync(x => x.Id == created.Id.Value);
        Assert.Equal(StokTalepDurumlari.Bekliyor, talep.Durum);
    }

    [Fact]
    public async Task UpdateSatirlarAsync_BekleyenTalepteKismiOnayDurumunuYazar()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var service = CreateService(dbContext);

        var created = await CreateBekleyenTalepAsync(service);
        var satir = Assert.Single(created.Satirlar);

        var updated = await service.UpdateSatirlarAsync(created.Id!.Value, new UpdateStokTalepSatirlarRequest
        {
            Satirlar =
            [
                new UpdateStokTalepSatirRequest
                {
                    Id = satir.Id!.Value,
                    TalepMiktari = 50,
                    OnaylananMiktar = 40
                }
            ]
        });

        Assert.Equal(StokTalepDurumlari.KismiOnaylandi, updated.Durum);
        Assert.Equal(40, Assert.Single(updated.Satirlar).OnaylananMiktar);
    }

    [Fact]
    public async Task TeslimEtAsync_OnayliTalepTransferOlustururVeTeslimEdildiYapar()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SeedSourceStockAsync(dbContext, 80);
        var service = CreateService(dbContext);

        var created = await CreateBekleyenTalepAsync(service);
        var satir = Assert.Single(created.Satirlar);
        var approved = await service.UpdateSatirlarAsync(created.Id!.Value, new UpdateStokTalepSatirlarRequest
        {
            Satirlar =
            [
                new UpdateStokTalepSatirRequest
                {
                    Id = satir.Id!.Value,
                    TalepMiktari = 50,
                    OnaylananMiktar = 40
                }
            ]
        });

        var delivered = await service.TeslimEtAsync(approved.Id!.Value, new TeslimEtStokTalepRequest());

        Assert.Equal(StokTalepDurumlari.TeslimEdildi, delivered.Durum);
        var deliveredSatir = Assert.Single(delivered.Satirlar);
        Assert.Equal(40, deliveredSatir.TeslimEdilenMiktar);
        Assert.NotNull(deliveredSatir.TransferGrupId);

        var hareketler = await dbContext.StokHareketleri
            .Where(x => x.KaynakModul == "StokTalepSatir" && x.KaynakId == deliveredSatir.Id)
            .OrderBy(x => x.Id)
            .ToListAsync();

        Assert.Equal(2, hareketler.Count);
        Assert.Equal([10, 20], hareketler.Select(x => x.DepoId).OrderBy(x => x).ToArray());
        Assert.All(hareketler, x => Assert.Equal(StokHareketTipleri.Transfer, x.HareketTipi));
    }

    [Fact]
    public async Task TeslimEtAsync_TransferTarihiniTeslimAnindanYazar()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SeedSourceStockAsync(dbContext, 80);
        var service = CreateService(dbContext);

        var created = await CreateBekleyenTalepAsync(service);
        var satir = Assert.Single(created.Satirlar);
        var approved = await service.UpdateSatirlarAsync(created.Id!.Value, new UpdateStokTalepSatirlarRequest
        {
            Satirlar =
            [
                new UpdateStokTalepSatirRequest
                {
                    Id = satir.Id!.Value,
                    TalepMiktari = 50,
                    OnaylananMiktar = 40
                }
            ]
        });

        var beforeDelivery = DateTime.UtcNow;
        await service.TeslimEtAsync(approved.Id!.Value, new TeslimEtStokTalepRequest());
        var afterDelivery = DateTime.UtcNow;

        var hareketler = await dbContext.StokHareketleri
            .Where(x => x.KaynakModul == "StokTalepSatir" && x.KaynakId == satir.Id)
            .ToListAsync();

        Assert.Equal(2, hareketler.Count);
        Assert.All(hareketler, hareket =>
        {
            Assert.NotEqual(created.TalepTarihi, hareket.HareketTarihi);
            Assert.NotEqual(created.TalepTarihi, hareket.BelgeTarihi);
            Assert.InRange(hareket.HareketTarihi, beforeDelivery.AddSeconds(-1), afterDelivery.AddSeconds(1));
            Assert.Equal(hareket.HareketTarihi, hareket.BelgeTarihi);
        });
    }

    [Fact]
    public async Task TeslimEtAsync_KaynakStokYetersizseRollbackYapar()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SeedSourceStockAsync(dbContext, 5);
        var service = CreateService(dbContext);

        var created = await CreateBekleyenTalepAsync(service);
        var satir = Assert.Single(created.Satirlar);
        var approved = await service.UpdateSatirlarAsync(created.Id!.Value, new UpdateStokTalepSatirlarRequest
        {
            Satirlar =
            [
                new UpdateStokTalepSatirRequest
                {
                    Id = satir.Id!.Value,
                    TalepMiktari = 50,
                    OnaylananMiktar = 40
                }
            ]
        });

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.TeslimEtAsync(approved.Id!.Value, new TeslimEtStokTalepRequest()));

        Assert.Equal(400, ex.ErrorCode);
        Assert.Equal("Kaynak depoda transfer için yeterli stok bulunmamaktadır.", ex.Message);

        var reloaded = await service.GetByIdAsync(approved.Id.Value);
        Assert.NotNull(reloaded);
        Assert.Equal(StokTalepDurumlari.KismiOnaylandi, reloaded!.Durum);
        Assert.Equal(0, Assert.Single(reloaded.Satirlar).TeslimEdilenMiktar);
        var hareketler = await dbContext.StokHareketleri.ToListAsync();
        Assert.Single(hareketler);
        Assert.DoesNotContain(hareketler, x => x.KaynakModul == "StokTalepSatir");
    }

    [Fact]
    public async Task TeslimEtAsync_IkinciKezTeslimiReddeder()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SeedSourceStockAsync(dbContext, 80);
        var service = CreateService(dbContext);

        var created = await CreateBekleyenTalepAsync(service);
        var satir = Assert.Single(created.Satirlar);
        var approved = await service.UpdateSatirlarAsync(created.Id!.Value, new UpdateStokTalepSatirlarRequest
        {
            Satirlar =
            [
                new UpdateStokTalepSatirRequest
                {
                    Id = satir.Id!.Value,
                    TalepMiktari = 50,
                    OnaylananMiktar = 40
                }
            ]
        });

        await service.TeslimEtAsync(approved.Id!.Value, new TeslimEtStokTalepRequest());

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.TeslimEtAsync(approved.Id.Value, new TeslimEtStokTalepRequest()));

        Assert.Equal(400, ex.ErrorCode);
        Assert.Equal("Sadece onaylanmis stok talepleri teslim edilebilir.", ex.Message);
    }

    [Fact]
    public void MigrationAssembly_AddStockRequests_DiscoverEdilir()
    {
        var options = new DbContextOptionsBuilder<StysAppDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=StysMigrationDiscovery;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        using var dbContext = new StysAppDbContext(options, new FakeCurrentUserAccessor(), new FakeCurrentTenantAccessor())
        {
            AllowExplicitTenantWritesWithoutAmbientTenant = true
        };
        var migrationsAssembly = dbContext.GetService<IMigrationsAssembly>();

        Assert.True(migrationsAssembly.Migrations.ContainsKey("20260823225839_AddStockRequests"));
    }

    [Fact]
    public async Task CreateTransferWithinCurrentTransactionAsync_MevcutTransactionIcindeCalisir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SeedSourceStockAsync(dbContext, 20);
        var stokHareketService = CreateStokHareketService(dbContext);

        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        var created = await stokHareketService.CreateTransferWithinCurrentTransactionAsync(new StokTransferRequest
        {
            KaynakDepoId = 10,
            HedefDepoId = 20,
            TasinirKartId = 100,
            HareketTarihi = new DateTime(2026, 8, 24, 9, 30, 0),
            BelgeTarihi = new DateTime(2026, 8, 24, 9, 30, 0),
            Miktar = 5,
            BirimFiyat = 0,
            Aciklama = "Transaction ici transfer"
        });
        await transaction.CommitAsync();

        Assert.Equal(2, created.Count);
        Assert.Equal(3, await dbContext.StokHareketleri.CountAsync());
    }

    private static async Task<StokTalepDto> CreateBekleyenTalepAsync(StokTalepService service)
    {
        var created = await service.AddAsync(new StokTalepDto
        {
            TalepEdenDepoId = 20,
            KarsilayanDepoId = 10,
            TalepTarihi = new DateTime(2026, 8, 23, 9, 0, 0),
            Aciklama = "Deterjan talebi"
        });

        var withRow = await service.AddSatirAsync(created.Id!.Value, new AddStokTalepSatirRequest
        {
            TasinirKartId = 100,
            TalepMiktari = 50
        });

        return await service.GonderAsync(withRow.Id!.Value);
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

    private static StokTalepService CreateService(StysAppDbContext dbContext)
    {
        var mapperConfig = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<StokTalepProfile>();
            cfg.AddProfile<StokHareketProfile>();
            cfg.AddProfile<TasinirKartProfile>();
        }, NullLoggerFactory.Instance);

        var mapper = mapperConfig.CreateMapper();
        var muhasebeDonemService = new FakeMuhasebeDonemService();
        return new StokTalepService(
            dbContext,
            new StokTalepRepository(dbContext, mapper),
            new DepoRepository(dbContext, mapper),
            new TasinirKartRepository(dbContext, mapper),
            new FakeUserAccessScopeService(DomainAccessScope.Scoped([], [1], [])),
            new FakeCurrentUserAccessor(),
            CreateStokHareketService(dbContext, mapper, muhasebeDonemService),
            mapper);
    }

    private static StokHareketService CreateStokHareketService(StysAppDbContext dbContext)
    {
        var mapper = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<StokHareketProfile>();
        }, NullLoggerFactory.Instance).CreateMapper();

        return CreateStokHareketService(dbContext, mapper, new FakeMuhasebeDonemService());
    }

    private static StokHareketService CreateStokHareketService(StysAppDbContext dbContext, IMapper mapper, IMuhasebeDonemService muhasebeDonemService)
        => new(
            dbContext,
            new StokHareketRepository(dbContext, mapper),
            new DepoRepository(dbContext, mapper),
            new TasinirKartRepository(dbContext, mapper),
            new CariKartRepository(dbContext, mapper),
            muhasebeDonemService,
            new FakeUserAccessScopeService(DomainAccessScope.Scoped([], [1], [])),
            new FakeKdvUygulamaService(),
            CreatePolicyService(dbContext, muhasebeDonemService),
            new StokMaliyetStrategyResolver([new AgirlikliOrtalamaMaliyetStrategy(dbContext), new FifoMaliyetStrategy(dbContext), new LifoMaliyetStrategy(dbContext)]),
            mapper);

    private static IStokMaliyetPolitikasiService CreatePolicyService(StysAppDbContext dbContext, IMuhasebeDonemService muhasebeDonemService)
        => new StokMaliyetPolitikasiService(
            dbContext,
            muhasebeDonemService,
            new FakeMuhasebeTesisScopeService(),
            new FakeUserAccessScopeService(DomainAccessScope.Scoped([], [1], [])),
            new StokHareketRepository(dbContext, new MapperConfiguration(cfg => cfg.AddProfile<StokHareketProfile>(), NullLoggerFactory.Instance).CreateMapper()));

    private static async Task SeedBaseAsync(StysAppDbContext dbContext)
    {
        dbContext.Kurumlar.Add(new Kurum
        {
            Id = 1,
            Kod = "TRT",
            Ad = "TRT",
            AktifMi = true
        });

        dbContext.Iller.Add(new Il
        {
            Id = 1,
            Ad = "Ankara",
            AktifMi = true
        });

        dbContext.Tesisler.Add(new Tesis
        {
            Id = 1,
            KurumId = 1,
            IlId = 1,
            Ad = "Tesis 1",
            Telefon = "000",
            Adres = "Adres 1",
            AktifMi = true
        });

        dbContext.MuhasebeDonemler.Add(new MuhasebeDonem
        {
            Id = 1,
            TesisId = 1,
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
            Ad = "Stok Hesabi",
            SeviyeNo = 2,
            HesapTipi = HesapTipi.DetayHesap,
            AktifMi = true,
            DetayHesapMi = true,
            HareketGorebilirMi = true,
            TesisId = 1
        });

        dbContext.TasinirKodlar.Add(new TasinirKod
        {
            Id = 200,
            TamKod = "150.01.01",
            Kod = "1500101",
            Ad = "Temizlik Malzemeleri",
            DuzeyNo = 3,
            AktifMi = true
        });

        dbContext.Depolar.AddRange(
            new Depo
            {
                Id = 10,
                TesisId = 1,
                Kod = "D-001",
                Ad = "Merkez Depo",
                AktifMi = true,
                MuhasebeHesapPlaniId = 1,
                MalzemeKayitTipi = DepoMalzemeKayitTipleri.MalzemeleriAyriKayittaTut
            },
            new Depo
            {
                Id = 20,
                TesisId = 1,
                Kod = "D-002",
                Ad = "Temizlik Deposu",
                AktifMi = true,
                MuhasebeHesapPlaniId = 1,
                MalzemeKayitTipi = DepoMalzemeKayitTipleri.MalzemeleriAyriKayittaTut
            });

        dbContext.TasinirKartlar.Add(new TasinirKart
        {
            Id = 100,
            TesisId = 1,
            TasinirKodId = 200,
            MuhasebeHesapPlaniId = 1,
            StokKodu = "STK-100",
            Ad = "Deterjan",
            Birim = "Adet",
            MalzemeTipi = MalzemeTipleri.Diger,
            TakipliMi = false,
            TakipTipi = TasinirKartTakipTipleri.Yok,
            KdvOrani = 20,
            AktifMi = true
        });

        dbContext.StokMaliyetPolitikalari.Add(new StokMaliyetPolitikasi
        {
            TesisId = 1,
            MaliYil = 2026,
            MaliyetYontemi = StokMaliyetYontemleri.AgirlikliOrtalama
        });

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedSourceStockAsync(StysAppDbContext dbContext, decimal miktar)
    {
        dbContext.StokHareketleri.Add(new StokHareket
        {
            DepoId = 10,
            TasinirKartId = 100,
            HareketTarihi = new DateTime(2026, 8, 22),
            HareketTipi = StokHareketTipleri.Giris,
            Miktar = miktar,
            BirimFiyat = 1,
            Tutar = miktar,
            Durum = StokHareketDurumlari.Aktif,
            KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli,
            KdvOrani = 20,
            KdvTutari = Math.Round(miktar * 0.2m, 2, MidpointRounding.AwayFromZero)
        });

        await dbContext.SaveChangesAsync();
    }

    private sealed class FakeMuhasebeDonemService : IMuhasebeDonemService
    {
        public Task<MuhasebeDonemDto?> GetAktifDonemAsync(int tesisId, DateTime tarih, CancellationToken cancellationToken = default)
            => Task.FromResult<MuhasebeDonemDto?>(new MuhasebeDonemDto
            {
                Id = 1,
                TesisId = tesisId,
                BaslangicTarihi = new DateTime(tarih.Year, tarih.Month, 1),
                BitisTarihi = new DateTime(tarih.Year, tarih.Month, DateTime.DaysInMonth(tarih.Year, tarih.Month)),
                KapaliMi = false,
                MaliYil = tarih.Year,
                DonemNo = tarih.Month
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

    private sealed class FakeUserAccessScopeService : IUserAccessScopeService
    {
        private readonly DomainAccessScope _scope;

        public FakeUserAccessScopeService(DomainAccessScope scope) => _scope = scope;

        public Task<DomainAccessScope> GetCurrentScopeAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_scope);
    }

    private sealed class FakeCurrentUserAccessor : ICurrentUserAccessor
    {
        public string? GetCurrentUserName() => "test-user";

        public Guid? GetCurrentUserId() => Guid.Parse("11111111-1111-1111-1111-111111111111");
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
}
