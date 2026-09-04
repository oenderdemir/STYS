using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.CariKartlar.Dtos;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.CariKartlar.Services;
using STYS.Muhasebe.Kdv.Dtos;
using STYS.Muhasebe.Kdv.Entities;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.Kdv.Services;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using STYS.Tesisler.Dto;
using STYS.Tesisler.Entities;
using STYS.Tesisler.Services;
using STYS.TicariBelgeler.Dtos;
using STYS.TicariBelgeler.Services;
using TOD.Platform.Identity.Users.DTO;
using TOD.Platform.Persistence.Rdbms.Paging;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// TicariBelgeLookupService'in (bkz. görev A/B) kendi iş kuralı katmanını - tesis-scope 403,
/// cari tipi/yön filtrelemesi, KDV istisna Kdvli/Tevkifatli-boş kuralı ve tarih-geçerlilik
/// filtresi - GERÇEK bir SQL Server/veritabanı GEREKTİRMEDEN, sahte (fake) alt servislerle
/// izole olarak doğrular. GetIadeAdaylariAsync/GetKaynakSatirlarAsync (ham SQL kullandığı için)
/// bu dosyada DEĞİL, ayrı bir [IntegrationFact] test dosyasında doğrulanır.
/// </summary>
public class TicariBelgeLookupServiceTests
{
    private static StysAppDbContext CreateUnusedDbContext()
    {
        var options = new DbContextOptionsBuilder<StysAppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new StysAppDbContext(options, new FakeCurrentUserAccessor(), new FakeCurrentTenantAccessor());
    }

    private static TicariBelgeLookupService CreateService(
        FakeTesisService? tesisService = null,
        FakeCariKartService? cariKartService = null,
        FakeKdvIstisnaTanimService? kdvService = null,
        DomainAccessScope? scope = null)
    {
        return new TicariBelgeLookupService(
            tesisService ?? new FakeTesisService([]),
            cariKartService ?? new FakeCariKartService([]),
            kdvService ?? new FakeKdvIstisnaTanimService([]),
            new FakeUserAccessScopeService(scope ?? DomainAccessScope.Unscoped()),
            new FakeCurrentTenantAccessor(),
            CreateUnusedDbContext());
    }

    // ── Cari kart lookup — tesis-scope 403 ──

    [Fact]
    public async Task GetCariKartlarAsync_KapsamDisiTesisId_403Firlatir()
    {
        var service = CreateService(scope: DomainAccessScope.Scoped([], [1, 2], []));

        var ex = await Assert.ThrowsAsync<BaseException>(
            () => service.GetCariKartlarAsync(999, SatisBelgesiTipi.SatisFaturasi));

        Assert.Equal(403, ex.ErrorCode);
    }

    [Fact]
    public async Task GetCariKartlarAsync_BosKapsam_403Firlatir()
    {
        var service = CreateService(scope: DomainAccessScope.Scoped([], [], []));

        await Assert.ThrowsAsync<BaseException>(() => service.GetCariKartlarAsync(1, SatisBelgesiTipi.SatisFaturasi));
    }

    // ── Cari kart lookup — belge yönüne göre cari tipi filtrelemesi ──

    [Fact]
    public async Task GetCariKartlarAsync_SatisYonu_YalnizcaMusteriVeKurumsalMusteriDonderir()
    {
        var cariler = new List<CariKartDto>
        {
            OrnekCari(1, "Musteri"), OrnekCari(2, "KurumsalMusteri"), OrnekCari(3, "Tedarikci")
        };
        var service = CreateService(cariKartService: new FakeCariKartService(cariler));

        var sonuc = await service.GetCariKartlarAsync(1, SatisBelgesiTipi.SatisFaturasi);

        Assert.Equal(2, sonuc.Count);
        Assert.DoesNotContain(sonuc, c => c.CariTipi == "Tedarikci");
    }

    [Fact]
    public async Task GetCariKartlarAsync_AlisYonu_YalnizcaTedarikciDonderir()
    {
        var cariler = new List<CariKartDto>
        {
            OrnekCari(1, "Musteri"), OrnekCari(2, "KurumsalMusteri"), OrnekCari(3, "Tedarikci")
        };
        var service = CreateService(cariKartService: new FakeCariKartService(cariler));

        var sonuc = await service.GetCariKartlarAsync(1, SatisBelgesiTipi.AlisFaturasi);

        Assert.Single(sonuc);
        Assert.Equal("Tedarikci", sonuc[0].CariTipi);
    }

    [Fact]
    public async Task GetCariKartlarAsync_PasifCari_Filtrelenir()
    {
        var pasif = OrnekCari(1, "Musteri");
        pasif.AktifMi = false;
        var service = CreateService(cariKartService: new FakeCariKartService([pasif, OrnekCari(2, "Musteri")]));

        var sonuc = await service.GetCariKartlarAsync(1, SatisBelgesiTipi.SatisFaturasi);

        Assert.Single(sonuc);
        Assert.Equal(2, sonuc[0].Id);
    }

    // ── KDV istisna lookup — Kdvli/Tevkifatli için sonuç YOK ──

    [Theory]
    [InlineData(KdvUygulamaTipi.Kdvli)]
    [InlineData(KdvUygulamaTipi.Tevkifatli)]
    public async Task GetKdvIstisnalarAsync_KdvliVeyaTevkifatli_BosListeDonderVeAltServisiHicCagirmaz(KdvUygulamaTipi tip)
    {
        var fakeKdv = new FakeKdvIstisnaTanimService([OrnekKdvIstisna(1, "K1")]);
        var service = CreateService(kdvService: fakeKdv);

        var sonuc = await service.GetKdvIstisnalarAsync(new TicariBelgeKdvIstisnaLookupFilterDto
        {
            BelgeTipi = SatisBelgesiTipi.SatisFaturasi,
            KdvUygulamaTipi = tip,
            BelgeTarihi = new DateTime(2026, 1, 1)
        });

        Assert.Empty(sonuc);
        Assert.Null(fakeKdv.SonFiltre);
    }

    [Fact]
    public async Task GetKdvIstisnalarAsync_AlisYonu_FiltreyeAlisIslemlerindeKullanilirMiGonderir()
    {
        var fakeKdv = new FakeKdvIstisnaTanimService([OrnekKdvIstisna(1, "K1")]);
        var service = CreateService(kdvService: fakeKdv);

        await service.GetKdvIstisnalarAsync(new TicariBelgeKdvIstisnaLookupFilterDto
        {
            BelgeTipi = SatisBelgesiTipi.AlisFaturasi,
            KdvUygulamaTipi = KdvUygulamaTipi.TamIstisna,
            BelgeTarihi = new DateTime(2026, 1, 1)
        });

        Assert.True(fakeKdv.SonFiltre!.AlisIslemlerindeKullanilirMi);
        Assert.Null(fakeKdv.SonFiltre!.SatisIslemlerindeKullanilirMi);
    }

    [Fact]
    public async Task GetKdvIstisnalarAsync_GecerlilikTarihiDisindakiTanim_Filtrelenir()
    {
        var geçerliDisiTanim = OrnekKdvIstisna(1, "ESKI");
        geçerliDisiTanim.GecerlilikBitisTarihi = new DateTime(2025, 12, 31);
        var geçerliTanim = OrnekKdvIstisna(2, "GUNCEL");

        var fakeKdv = new FakeKdvIstisnaTanimService([geçerliDisiTanim, geçerliTanim]);
        var service = CreateService(kdvService: fakeKdv);

        var sonuc = await service.GetKdvIstisnalarAsync(new TicariBelgeKdvIstisnaLookupFilterDto
        {
            BelgeTipi = SatisBelgesiTipi.SatisFaturasi,
            KdvUygulamaTipi = KdvUygulamaTipi.TamIstisna,
            BelgeTarihi = new DateTime(2026, 1, 1)
        });

        Assert.Single(sonuc);
        Assert.Equal("GUNCEL", sonuc[0].Kod);
    }

    // ── Yardımcılar ──

    private static CariKartDto OrnekCari(int id, string cariTipi) => new()
    {
        Id = id,
        CariKodu = $"C{id}",
        CariTipi = cariTipi,
        UnvanAdSoyad = $"Cari {id}",
        AktifMi = true
    };

    private static KdvIstisnaTanimDto OrnekKdvIstisna(int id, string kod) => new()
    {
        Id = id,
        Kod = kod,
        Ad = $"Istisna {kod}",
        UygulamaTipi = KdvUygulamaTipi.TamIstisna,
        AktifMi = true,
        SatisIslemlerindeKullanilirMi = true,
        AlisIslemlerindeKullanilirMi = true
    };

    // ── Fake'ler ──

    private sealed class FakeCurrentUserAccessor : ICurrentUserAccessor
    {
        public string? GetCurrentUserName() => "test";
        public Guid? GetCurrentUserId() => Guid.NewGuid();
    }

    private sealed class FakeCurrentTenantAccessor : ICurrentTenantAccessor
    {
        public int? GetCurrentKurumId() => 1;
        public IReadOnlyList<int> GetAccessibleKurumIds() => [1];
        public bool IsSuperAdmin() => false;
        public bool IsKurumAdmin() => false;
    }

    private sealed class FakeUserAccessScopeService(DomainAccessScope scope) : IUserAccessScopeService
    {
        public Task<DomainAccessScope> GetCurrentScopeAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(scope);
    }

    private sealed class FakeTesisService(List<TesisDto> tesisler) : ITesisService
    {
        public Task<List<TesisDto>> GetAktifKurumTesisleriAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(tesisler);

        public Task<UserDto> CreateTesisYoneticisiUserAsync(int tesisId, UserDto dto) => throw new NotImplementedException();
        public Task<UserDto> CreateResepsiyonistUserAsync(int tesisId, UserDto dto) => throw new NotImplementedException();
        public Task<UserDto> CreateBinaYoneticisiUserAsync(int tesisId, UserDto dto) => throw new NotImplementedException();
        public Task<UserDto> CreateRestoranYoneticisiUserAsync(int tesisId, UserDto dto) => throw new NotImplementedException();
        public Task<UserDto> CreateRestoranGarsonuUserAsync(int tesisId, UserDto dto) => throw new NotImplementedException();
        public Task<UserDto> CreateMuhasebeciUserAsync(int tesisId, UserDto dto) => throw new NotImplementedException();
        public Task<IEnumerable<TesisDto>> GetAllAsync(Func<IQueryable<Tesis>, IQueryable<Tesis>>? include = null) => throw new NotImplementedException();
        public Task<TesisDto?> GetByIdAsync(int id, Func<IQueryable<Tesis>, IQueryable<Tesis>>? include = null) => throw new NotImplementedException();
        public Task<PagedResult<TesisDto>> GetPagedAsync(PagedRequest request, Expression<Func<Tesis, bool>>? predicate = null, Func<IQueryable<Tesis>, IQueryable<Tesis>>? include = null, Func<IQueryable<Tesis>, IOrderedQueryable<Tesis>>? orderBy = null) => throw new NotImplementedException();
        public Task<TesisDto> AddAsync(TesisDto dto) => throw new NotImplementedException();
        public Task<TesisDto> UpdateAsync(TesisDto dto) => throw new NotImplementedException();
        public Task DeleteAsync(int id) => throw new NotImplementedException();
        public Task<IEnumerable<TesisDto>> WhereAsync(Expression<Func<Tesis, bool>> predicate, Func<IQueryable<Tesis>, IQueryable<Tesis>>? include = null) => throw new NotImplementedException();
        public Task<bool> AnyAsync(Expression<Func<Tesis, bool>> predicate, Func<IQueryable<Tesis>, IQueryable<Tesis>>? include = null) => throw new NotImplementedException();
    }

    private sealed class FakeCariKartService(List<CariKartDto> cariler) : ICariKartService
    {
        public Task<IEnumerable<CariKartDto>> GetAllAsync(int? tesisId, Func<IQueryable<CariKart>, IQueryable<CariKart>>? include = null)
            => Task.FromResult<IEnumerable<CariKartDto>>(cariler);

        public Task<PagedResult<CariKartDto>> GetPagedAsync(PagedRequest request, int? tesisId, Func<IQueryable<CariKart>, IQueryable<CariKart>>? include = null, Func<IQueryable<CariKart>, IOrderedQueryable<CariKart>>? orderBy = null) => throw new NotImplementedException();
        public Task<CariBakiyeDto> GetBakiyeAsync(int cariKartId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<CariKartDto> CariKartAcilisBakiyesiDuzeltAsync(int cariKartId, decimal yeniTutar, string? yeniYonu, DateTime? duzeltmeTarihi = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IEnumerable<CariKartDto>> GetAllAsync(Func<IQueryable<CariKart>, IQueryable<CariKart>>? include = null) => throw new NotImplementedException();
        public Task<CariKartDto?> GetByIdAsync(int id, Func<IQueryable<CariKart>, IQueryable<CariKart>>? include = null) => throw new NotImplementedException();
        public Task<PagedResult<CariKartDto>> GetPagedAsync(PagedRequest request, Expression<Func<CariKart, bool>>? predicate = null, Func<IQueryable<CariKart>, IQueryable<CariKart>>? include = null, Func<IQueryable<CariKart>, IOrderedQueryable<CariKart>>? orderBy = null) => throw new NotImplementedException();
        public Task<CariKartDto> AddAsync(CariKartDto dto) => throw new NotImplementedException();
        public Task<CariKartDto> UpdateAsync(CariKartDto dto) => throw new NotImplementedException();
        public Task DeleteAsync(int id) => throw new NotImplementedException();
        public Task<CariKartDto> FindOrCreateMusteriCariKartAsync(CariKartDto dto, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IEnumerable<CariKartDto>> WhereAsync(Expression<Func<CariKart, bool>> predicate, Func<IQueryable<CariKart>, IQueryable<CariKart>>? include = null) => throw new NotImplementedException();
        public Task<bool> AnyAsync(Expression<Func<CariKart, bool>> predicate, Func<IQueryable<CariKart>, IQueryable<CariKart>>? include = null) => throw new NotImplementedException();
    }

    private sealed class FakeKdvIstisnaTanimService(List<KdvIstisnaTanimDto> tanimlar) : IKdvIstisnaTanimService
    {
        public KdvIstisnaTanimFilterDto? SonFiltre { get; private set; }

        public Task<List<KdvIstisnaTanimDto>> FilterAsync(KdvIstisnaTanimFilterDto filter, CancellationToken cancellationToken = default)
        {
            SonFiltre = filter;
            var sonuc = tanimlar.Where(t =>
                (!filter.UygulamaTipi.HasValue || t.UygulamaTipi == filter.UygulamaTipi.Value) &&
                (!filter.AktifMi.HasValue || t.AktifMi == filter.AktifMi.Value) &&
                (!filter.SatisIslemlerindeKullanilirMi.HasValue || t.SatisIslemlerindeKullanilirMi == filter.SatisIslemlerindeKullanilirMi.Value) &&
                (!filter.AlisIslemlerindeKullanilirMi.HasValue || t.AlisIslemlerindeKullanilirMi == filter.AlisIslemlerindeKullanilirMi.Value))
                .ToList();
            return Task.FromResult(sonuc);
        }

        public Task<IEnumerable<KdvIstisnaTanimDto>> GetAllAsync(Func<IQueryable<KdvIstisnaTanim>, IQueryable<KdvIstisnaTanim>>? include = null) => throw new NotImplementedException();
        public Task<KdvIstisnaTanimDto?> GetByIdAsync(int id, Func<IQueryable<KdvIstisnaTanim>, IQueryable<KdvIstisnaTanim>>? include = null) => throw new NotImplementedException();
        public Task<PagedResult<KdvIstisnaTanimDto>> GetPagedAsync(PagedRequest request, Expression<Func<KdvIstisnaTanim, bool>>? predicate = null, Func<IQueryable<KdvIstisnaTanim>, IQueryable<KdvIstisnaTanim>>? include = null, Func<IQueryable<KdvIstisnaTanim>, IOrderedQueryable<KdvIstisnaTanim>>? orderBy = null) => throw new NotImplementedException();
        public Task<KdvIstisnaTanimDto> AddAsync(KdvIstisnaTanimDto dto) => throw new NotImplementedException();
        public Task<KdvIstisnaTanimDto> UpdateAsync(KdvIstisnaTanimDto dto) => throw new NotImplementedException();
        public Task DeleteAsync(int id) => throw new NotImplementedException();
        public Task<IEnumerable<KdvIstisnaTanimDto>> WhereAsync(Expression<Func<KdvIstisnaTanim, bool>> predicate, Func<IQueryable<KdvIstisnaTanim>, IQueryable<KdvIstisnaTanim>>? include = null) => throw new NotImplementedException();
        public Task<bool> AnyAsync(Expression<Func<KdvIstisnaTanim, bool>> predicate, Func<IQueryable<KdvIstisnaTanim>, IQueryable<KdvIstisnaTanim>>? include = null) => throw new NotImplementedException();
    }
}
