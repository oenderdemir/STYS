using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Iller.Entities;
using STYS.Kurumlar.Entities;
using STYS.Muhasebe.CariKartlar.Repositories;
using STYS.Muhasebe.Common.Services;
using STYS.Muhasebe.Depolar.Entities;
using STYS.Muhasebe.Depolar.Repositories;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.Kdv.Services;
using STYS.Muhasebe.Kdv.Dtos;
using STYS.Muhasebe.MuhasebeDonemleri.Dtos;
using STYS.Muhasebe.MuhasebeDonemleri.Entities;
using STYS.Muhasebe.MuhasebeDonemleri.Services;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;
using STYS.Muhasebe.StokCikis.Services;
using STYS.Muhasebe.StokHareketleri.Dtos;
using STYS.Muhasebe.StokHareketleri.Entities;
using STYS.Muhasebe.StokHareketleri.Mapping;
using STYS.Muhasebe.StokHareketleri.Repositories;
using STYS.Muhasebe.StokHareketleri.Services;
using STYS.Muhasebe.StokMaliyetPolitikalari.Dtos;
using STYS.Muhasebe.StokMaliyetPolitikalari.Entities;
using STYS.Muhasebe.StokMaliyetPolitikalari.Services;
using STYS.Muhasebe.StokTalepleri.Dtos;
using STYS.Muhasebe.StokTalepleri.Mapping;
using STYS.Muhasebe.StokTalepleri.Repositories;
using STYS.Muhasebe.StokTalepleri.Services;
using STYS.Muhasebe.TasinirKartlari.Entities;
using STYS.Muhasebe.TasinirKartlari.Mapping;
using STYS.Muhasebe.TasinirKartlari.Repositories;
using STYS.Muhasebe.TasinirKodlari.Entities;
using STYS.Tesisler;
using STYS.Tesisler.Entities;
using TOD.Platform.Persistence.Rdbms.Paging;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Tests;

public class StokCikisServiceTests
{
    [Fact]
    public void Resolver_TalepVeOnayStrategySecer()
    {
        var resolver = new StokCikisStrategyResolver([
            new FakeStrategy(StokCikisYontemleri.TalepVeOnay),
            new FakeStrategy(StokCikisYontemleri.DogrudanDepoCikisi)
        ]);

        var strategy = resolver.Resolve(StokCikisYontemleri.TalepVeOnay);

        Assert.Equal(StokCikisYontemleri.TalepVeOnay, strategy.Yontem);
    }

    [Fact]
    public void Resolver_DogrudanDepoCikisiStrategySecer()
    {
        var resolver = new StokCikisStrategyResolver([
            new FakeStrategy(StokCikisYontemleri.TalepVeOnay),
            new FakeStrategy(StokCikisYontemleri.DogrudanDepoCikisi)
        ]);

        var strategy = resolver.Resolve(StokCikisYontemleri.DogrudanDepoCikisi);

        Assert.Equal(StokCikisYontemleri.DogrudanDepoCikisi, strategy.Yontem);
    }

    [Fact]
    public void Resolver_BilinmeyenYontemdeFailFastCalisir()
    {
        var resolver = new StokCikisStrategyResolver([
            new FakeStrategy(StokCikisYontemleri.TalepVeOnay)
        ]);

        var ex = Assert.Throws<BaseException>(() => resolver.Resolve("Bilinmeyen"));

        Assert.Equal(400, ex.ErrorCode);
        Assert.Equal("Bilinmeyen stok çıkış yöntemi: Bilinmeyen", ex.Message);
    }

    [Fact]
    public async Task DogrudanTransferBaslatAsync_TalepVeOnayTesisindeBypassiReddeder()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext, StokCikisYontemleri.TalepVeOnay);
        await SeedSourceStockAsync(dbContext, 20);
        var service = CreateStokCikisService(dbContext);

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.DogrudanTransferBaslatAsync(CreateTransferRequest()));

        Assert.Equal(400, ex.ErrorCode);
        Assert.Equal("Bu tesiste stok çıkışı talep ve onay akışıyla yürütülmelidir.", ex.Message);
    }

    [Fact]
    public async Task TalepBaslatAsync_DogrudanDepoCikisindaTalepOlusturmayiReddeder()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext, StokCikisYontemleri.DogrudanDepoCikisi);
        var service = CreateStokCikisService(dbContext);

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.TalepBaslatAsync(new CreateStokTalepRequest
        {
            TalepEdenDepoId = 20,
            KarsilayanDepoId = 10,
            TalepTarihi = new DateTime(2026, 8, 24, 10, 0, 0),
            Aciklama = "Talep"
        }));

        Assert.Equal(400, ex.ErrorCode);
        Assert.Equal("Bu tesiste stok talepleri yerine doğrudan depo çıkışı kullanılmalıdır.", ex.Message);
    }

    [Fact]
    public async Task DogrudanTransferBaslatAsync_DogrudanModdaTalepOlusmadanTransferOlusturur()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext, StokCikisYontemleri.DogrudanDepoCikisi);
        await SeedSourceStockAsync(dbContext, 20);
        var service = CreateStokCikisService(dbContext);

        var created = await service.DogrudanTransferBaslatAsync(CreateTransferRequest(miktar: 5));

        Assert.Equal(2, created.Count);
        Assert.Empty(await dbContext.StokTalepler.ToListAsync());
        Assert.Equal(3, await dbContext.StokHareketleri.CountAsync());
    }

    [Fact]
    public async Task DogrudanTransferBaslatAsync_NegatifStokKuraliniMevcutTransferMotorundanKorur()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext, StokCikisYontemleri.DogrudanDepoCikisi);
        await SeedSourceStockAsync(dbContext, 3);
        var service = CreateStokCikisService(dbContext);

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.DogrudanTransferBaslatAsync(CreateTransferRequest(miktar: 5)));

        Assert.Equal(400, ex.ErrorCode);
        Assert.Equal("Kaynak depoda transfer için yeterli stok bulunmamaktadır.", ex.Message);
    }

    [Fact]
    public async Task DogrudanTransferBaslatAsync_SeriKuraliniMevcutTransferMotorundanKorur()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext, StokCikisYontemleri.DogrudanDepoCikisi, takipTipi: TasinirKartTakipTipleri.Seri);
        await SeedSourceStockAsync(dbContext, 1, stokSeriId: 500);
        var service = CreateStokCikisService(dbContext);

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.DogrudanTransferBaslatAsync(CreateTransferRequest(miktar: 2, stokSeriId: 500)));

        Assert.Equal(400, ex.ErrorCode);
        Assert.Equal("Seri takipli taşınır kartlarda miktar 1 olmalıdır.", ex.Message);
    }

    private static StokCikisService CreateStokCikisService(StysAppDbContext dbContext)
    {
        var mapper = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<StokTalepProfile>();
            cfg.AddProfile<StokHareketProfile>();
            cfg.AddProfile<TasinirKartProfile>();
        }, NullLoggerFactory.Instance).CreateMapper();
        var muhasebeDonemService = new FakeMuhasebeDonemService();
        var stokHareketService = new StokHareketService(
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
        var stokTalepService = new StokTalepService(
            dbContext,
            new StokTalepRepository(dbContext, mapper),
            new DepoRepository(dbContext, mapper),
            new TasinirKartRepository(dbContext, mapper),
            new FakeUserAccessScopeService(DomainAccessScope.Scoped([], [1], [])),
            new FakeCurrentUserAccessor(),
            stokHareketService,
            mapper);
        var resolver = new StokCikisStrategyResolver([
            new TalepVeOnayStokCikisStrategy(stokTalepService),
            new DogrudanDepoCikisStrategy(stokHareketService)
        ]);

        return new StokCikisService(dbContext, new DepoRepository(dbContext, mapper), resolver);
    }

    private static IStokMaliyetPolitikasiService CreatePolicyService(StysAppDbContext dbContext, IMuhasebeDonemService muhasebeDonemService)
        => new StokMaliyetPolitikasiService(
            dbContext,
            muhasebeDonemService,
            new FakeMuhasebeTesisScopeService(),
            new FakeUserAccessScopeService(DomainAccessScope.Scoped([], [1], [])),
            new StokHareketRepository(dbContext, new MapperConfiguration(cfg => cfg.AddProfile<StokHareketProfile>(), NullLoggerFactory.Instance).CreateMapper()));

    private static StokTransferRequest CreateTransferRequest(decimal miktar = 10, int? stokSeriId = null)
        => new()
        {
            KaynakDepoId = 10,
            HedefDepoId = 20,
            TasinirKartId = 100,
            HareketTarihi = new DateTime(2026, 8, 24, 9, 0, 0),
            BelgeTarihi = new DateTime(2026, 8, 24, 9, 0, 0),
            Miktar = miktar,
            BirimFiyat = 0,
            StokSeriId = stokSeriId
        };

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

    private static async Task SeedBaseAsync(StysAppDbContext dbContext, string stokCikisYontemi, string takipTipi = TasinirKartTakipTipleri.Yok)
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
            AktifMi = true,
            StokCikisYontemi = stokCikisYontemi
        });

        dbContext.Depolar.AddRange(
            new Depo
            {
                Id = 10,
                TesisId = 1,
                Kod = "MERKEZ",
                Ad = "Merkez Depo",
                AktifMi = true,
                MuhasebeHesapPlaniId = 1,
                MalzemeKayitTipi = DepoMalzemeKayitTipleri.MalzemeleriAyriKayittaTut
            },
            new Depo
            {
                Id = 20,
                TesisId = 1,
                Kod = "TEMIZLIK",
                Ad = "Temizlik Depo",
                AktifMi = true,
                MuhasebeHesapPlaniId = 1,
                MalzemeKayitTipi = DepoMalzemeKayitTipleri.MalzemeleriAyriKayittaTut
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
            Id = 1000,
            Kod = "150.01.001",
            Ad = "Temizlik Malzemeleri",
            AktifMi = true
        });

        dbContext.TasinirKartlar.Add(new TasinirKart
        {
            Id = 100,
            TesisId = 1,
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

        dbContext.StokMaliyetPolitikalari.Add(new StokMaliyetPolitikasi
        {
            Id = 1,
            TesisId = 1,
            MaliYil = 2026,
            MaliyetYontemi = StokMaliyetYontemleri.AgirlikliOrtalama
        });

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedSourceStockAsync(StysAppDbContext dbContext, decimal miktar, int? stokSeriId = null)
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
            StokSeriId = stokSeriId,
            KdvUygulamaTipi = (int)KdvUygulamaTipi.KdvKapsamDisi,
            KdvOrani = 0,
            KdvTutari = 0
        });

        await dbContext.SaveChangesAsync();
    }

    private sealed class FakeUserAccessScopeService : IUserAccessScopeService
    {
        private readonly DomainAccessScope _scope;

        public FakeUserAccessScopeService(DomainAccessScope scope)
        {
            _scope = scope;
        }

        public Task<DomainAccessScope> GetCurrentScopeAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_scope);
    }

    private sealed class FakeCurrentUserAccessor : ICurrentUserAccessor
    {
        public Guid? GetCurrentUserId() => Guid.NewGuid();
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

    private sealed class FakeStrategy : IStokCikisStrategy
    {
        public FakeStrategy(string yontem)
        {
            Yontem = yontem;
        }

        public string Yontem { get; }

        public Task<STYS.Muhasebe.StokCikis.Dtos.StokCikisSonuc> BaslatAsync(STYS.Muhasebe.StokCikis.Dtos.StokCikisIstegi istek, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }
}
