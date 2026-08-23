using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Iller.Entities;
using STYS.Kurumlar.Entities;
using STYS.Muhasebe.Common.Services;
using STYS.Muhasebe.Depolar.Entities;
using STYS.Muhasebe.Depolar.Repositories;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.StokHareketleri.Entities;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;
using STYS.Muhasebe.TasinirKartlari.Dtos;
using STYS.Muhasebe.TasinirKartlari.Entities;
using STYS.Muhasebe.TasinirKartlari.Mapping;
using STYS.Muhasebe.TasinirKartlari.Repositories;
using STYS.Muhasebe.TasinirKartlari.Services;
using STYS.Muhasebe.TasinirKodlari.Entities;
using STYS.Muhasebe.TasinirKodlari.Repositories;
using STYS.Muhasebe.TasinirKodMuhasebeHesapEslemeleri.Dtos;
using STYS.Muhasebe.TasinirKodMuhasebeHesapEslemeleri.Entities;
using STYS.Muhasebe.TasinirKodMuhasebeHesapEslemeleri.Services;
using STYS.Tesisler.Entities;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.Persistence.Rdbms.Paging;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Tests;

public class TasinirKartServiceTests
{
    [Fact]
    public async Task UpdateAsync_AyniTesistekiVarsayilanDepoyuKabulEder()
    {
        await using var dbContext = CreateDbContext();
        await SeedAsync(dbContext);
        var service = CreateService(dbContext, DomainAccessScope.Scoped([], [1], []));

        var dto = CreateUpdateDto(varsayilanDepoId: 10);

        var result = await service.UpdateAsync(dto);

        Assert.Equal(10, result.VarsayilanDepoId);
        var entity = await dbContext.TasinirKartlar.AsNoTracking().SingleAsync(x => x.Id == 100);
        Assert.Equal(10, entity.VarsayilanDepoId);
    }

    [Fact]
    public async Task UpdateAsync_FarkliTesistekiVarsayilanDepoyuReddeder()
    {
        await using var dbContext = CreateDbContext();
        await SeedAsync(dbContext);
        var service = CreateService(dbContext, DomainAccessScope.Scoped([], [1], []));

        var dto = CreateUpdateDto(varsayilanDepoId: 20);

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.UpdateAsync(dto));

        Assert.Equal(400, ex.ErrorCode);
        Assert.Equal("Varsayilan depo tasinir kart ile ayni tesise ait olmalidir.", ex.Message);
    }

    [Fact]
    public async Task UpdateAsync_StokBakiyesiVarkenTakipliMiDegisimiReddeder()
    {
        await using var dbContext = CreateDbContext();
        await SeedAsync(dbContext);
        dbContext.StokHareketleri.Add(new StokHareket
        {
            DepoId = 10,
            TasinirKartId = 100,
            HareketTarihi = new DateTime(2026, 8, 21),
            HareketTipi = StokHareketTipleri.Giris,
            Miktar = 5,
            BirimFiyat = 1,
            Tutar = 5,
            Durum = StokHareketDurumlari.Aktif,
            KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli,
            KdvOrani = 20,
            KdvTutari = 1
        });
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, DomainAccessScope.Scoped([], [1], []));

        var dto = CreateUpdateDto(varsayilanDepoId: 10);
        dto.TakipliMi = true;
        dto.TakipTipi = TasinirKartTakipTipleri.Lot;

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.UpdateAsync(dto));

        Assert.Equal(400, ex.ErrorCode);
        Assert.Equal("Stok bakiyesi bulunan taşınır kartın takip tipi değiştirilemez.", ex.Message);
    }

    private static TasinirKartDto CreateUpdateDto(int? varsayilanDepoId)
    {
        return new TasinirKartDto
        {
            Id = 100,
            TesisId = 1,
            TasinirKodId = 200,
            VarsayilanDepoId = varsayilanDepoId,
            Ad = "Finish Quantum",
            Birim = "Adet",
            MalzemeTipi = MalzemeTipleri.Diger,
            SarfMi = false,
            DemirbasMi = false,
            TakipliMi = false,
            TakipTipi = TasinirKartTakipTipleri.Yok,
            KdvOrani = 20,
            AktifMi = true,
            Aciklama = null
        };
    }

    private static StysAppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<StysAppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new StysAppDbContext(options, new FakeCurrentUserAccessor(), new FakeCurrentTenantAccessor())
        {
            AllowExplicitTenantWritesWithoutAmbientTenant = true
        };
    }

    private static TasinirKartService CreateService(StysAppDbContext dbContext, DomainAccessScope scope)
    {
        var mapperConfig = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<TasinirKartProfile>();
        }, NullLoggerFactory.Instance);

        var mapper = mapperConfig.CreateMapper();
        return new TasinirKartService(
            new TasinirKartRepository(dbContext, mapper),
            new TasinirKodRepository(dbContext, mapper),
            new DepoRepository(dbContext, mapper),
            new FakeUserAccessScopeService(scope),
            dbContext,
            mapper,
            new FakeMuhasebeDetayHesapService(),
            new FakeTasinirKodMuhasebeHesapEslemeService());
    }

    private static async Task SeedAsync(StysAppDbContext dbContext)
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

        dbContext.Tesisler.AddRange(
            new Tesis { Id = 1, KurumId = 1, IlId = 1, Ad = "Tesis 1", Telefon = "000", Adres = "Adres 1", AktifMi = true },
            new Tesis { Id = 2, KurumId = 1, IlId = 1, Ad = "Tesis 2", Telefon = "111", Adres = "Adres 2", AktifMi = true });

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
                Ad = "Temizlik Deposu",
                AktifMi = true,
                MalzemeKayitTipi = DepoMalzemeKayitTipleri.MalzemeleriAyriKayittaTut
            },
            new Depo
            {
                Id = 20,
                TesisId = 2,
                Kod = "D-002",
                Ad = "Ana Depo",
                AktifMi = true,
                MalzemeKayitTipi = DepoMalzemeKayitTipleri.MalzemeleriAyriKayittaTut
            });

        dbContext.TasinirKartlar.Add(new TasinirKart
        {
            Id = 100,
            TesisId = 1,
            TasinirKodId = 200,
            StokKodu = "STK-100",
            Ad = "Finish Quantum",
            Birim = "Adet",
            MalzemeTipi = MalzemeTipleri.Diger,
            KdvOrani = 20,
            AktifMi = true
        });

        await dbContext.SaveChangesAsync();
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

        public Guid? GetCurrentUserId() => Guid.NewGuid();
    }

    private sealed class FakeCurrentTenantAccessor : ICurrentTenantAccessor
    {
        public int? GetCurrentKurumId() => 1;

        public IReadOnlyList<int> GetAccessibleKurumIds() => [1];

        public bool IsSuperAdmin() => false;

        public bool IsKurumAdmin() => false;
    }

    private sealed class FakeMuhasebeDetayHesapService : IMuhasebeDetayHesapService
    {
        public Task<MuhasebeDetayHesapSonuc> CreateOrResolveDetayHesapAsync(
            int tesisId,
            string anaMuhasebeHesapKodu,
            string kaynakTipi,
            string kaynakAd,
            int? kaynakId = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new MuhasebeDetayHesapSonuc
            {
                MuhasebeHesapPlaniId = 1,
                Kod = "STK-NEW",
                AnaMuhasebeHesapKodu = anaMuhasebeHesapKodu,
                SiraNo = 1
            });
        }
    }

    private sealed class FakeTasinirKodMuhasebeHesapEslemeService : ITasinirKodMuhasebeHesapEslemeService
    {
        public Task<List<TasinirKodMuhasebeHesapEslemeDto>> GetByTasinirKodIdAsync(int tasinirKodId, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<TasinirKodMuhasebeHesapEslemeDto>());

        public Task<TasinirKodMuhasebeHesapEslemeDto?> GetVarsayilanAsync(int tasinirKodId, string malzemeTipi, string hareketTipi, CancellationToken cancellationToken = default)
            => Task.FromResult<TasinirKodMuhasebeHesapEslemeDto?>(new TasinirKodMuhasebeHesapEslemeDto { MuhasebeHesapPlaniId = 1 });

        public Task<IEnumerable<TasinirKodMuhasebeHesapEslemeDto>> GetAllAsync(Func<IQueryable<TasinirKodMuhasebeHesapEsleme>, IQueryable<TasinirKodMuhasebeHesapEsleme>>? include = null)
            => throw new NotSupportedException();

        public Task<TasinirKodMuhasebeHesapEslemeDto?> GetByIdAsync(int id, Func<IQueryable<TasinirKodMuhasebeHesapEsleme>, IQueryable<TasinirKodMuhasebeHesapEsleme>>? include = null)
            => throw new NotSupportedException();

        public Task<PagedResult<TasinirKodMuhasebeHesapEslemeDto>> GetPagedAsync(PagedRequest request, System.Linq.Expressions.Expression<Func<TasinirKodMuhasebeHesapEsleme, bool>>? predicate = null, Func<IQueryable<TasinirKodMuhasebeHesapEsleme>, IQueryable<TasinirKodMuhasebeHesapEsleme>>? include = null, Func<IQueryable<TasinirKodMuhasebeHesapEsleme>, IOrderedQueryable<TasinirKodMuhasebeHesapEsleme>>? orderBy = null)
            => throw new NotSupportedException();

        public Task<TasinirKodMuhasebeHesapEslemeDto> AddAsync(TasinirKodMuhasebeHesapEslemeDto dto)
            => throw new NotSupportedException();

        public Task<TasinirKodMuhasebeHesapEslemeDto> UpdateAsync(TasinirKodMuhasebeHesapEslemeDto dto)
            => throw new NotSupportedException();

        public Task DeleteAsync(int id)
            => throw new NotSupportedException();

        public Task<IEnumerable<TasinirKodMuhasebeHesapEslemeDto>> WhereAsync(System.Linq.Expressions.Expression<Func<TasinirKodMuhasebeHesapEsleme, bool>> predicate, Func<IQueryable<TasinirKodMuhasebeHesapEsleme>, IQueryable<TasinirKodMuhasebeHesapEsleme>>? include = null)
            => throw new NotSupportedException();

        public Task<bool> AnyAsync(System.Linq.Expressions.Expression<Func<TasinirKodMuhasebeHesapEsleme, bool>> predicate, Func<IQueryable<TasinirKodMuhasebeHesapEsleme>, IQueryable<TasinirKodMuhasebeHesapEsleme>>? include = null)
            => throw new NotSupportedException();
    }
}
