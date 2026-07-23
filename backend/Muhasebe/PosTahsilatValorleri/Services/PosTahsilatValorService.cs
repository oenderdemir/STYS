using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.Common.Services;
using STYS.Muhasebe.PosTahsilatValorleri.Dtos;
using STYS.Muhasebe.PosTahsilatValorleri.Entities;
using STYS.Muhasebe.PosTahsilatValorleri.Repositories;
using TOD.Platform.Persistence.Rdbms.Paging;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.Muhasebe.PosTahsilatValorleri.Services;

public class PosTahsilatValorService : BaseRdbmsService<PosTahsilatValorDto, PosTahsilatValor, int>, IPosTahsilatValorService
{
    private readonly IPosTahsilatValorRepository _repository;
    private readonly StysAppDbContext _dbContext;
    private readonly IUserAccessScopeService _userAccessScopeService;
    private readonly IValorTarihHesaplamaService _valorTarihHesaplamaService;

    public PosTahsilatValorService(
        IPosTahsilatValorRepository repository,
        StysAppDbContext dbContext,
        IUserAccessScopeService userAccessScopeService,
        IValorTarihHesaplamaService valorTarihHesaplamaService,
        IMapper mapper)
        : base(repository, mapper)
    {
        _repository = repository;
        _dbContext = dbContext;
        _userAccessScopeService = userAccessScopeService;
        _valorTarihHesaplamaService = valorTarihHesaplamaService;
    }

    public override async Task<PosTahsilatValorDto?> GetByIdAsync(int id, Func<IQueryable<PosTahsilatValor>, IQueryable<PosTahsilatValor>>? include = null)
    {
        var dto = await base.GetByIdAsync(id, BuildIncludeQuery(include));
        if (dto is not null)
        {
            Zenginlestir(dto);
        }
        return dto;
    }

    public override async Task<PagedResult<PosTahsilatValorDto>> GetPagedAsync(
        PagedRequest request,
        System.Linq.Expressions.Expression<Func<PosTahsilatValor, bool>>? predicate = null,
        Func<IQueryable<PosTahsilatValor>, IQueryable<PosTahsilatValor>>? include = null,
        Func<IQueryable<PosTahsilatValor>, IOrderedQueryable<PosTahsilatValor>>? orderBy = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var scopedPredicate = BuildScopedPredicate(scope, predicate);
        var result = await base.GetPagedAsync(request, scopedPredicate, BuildIncludeQuery(include), orderBy);
        foreach (var item in result.Items)
        {
            Zenginlestir(item);
        }
        return result;
    }

    public async Task<PosTahsilatValorOzetDto> GetOzetAsync(int? tesisId, CancellationToken cancellationToken = default)
    {
        var bugun = ValorTarihHesaplamaService.BugunIstanbul();
        var query = _dbContext.PosTahsilatValorleri.AsNoTracking().Where(x => !x.IsDeleted);
        if (tesisId.HasValue)
        {
            query = query.Where(x => x.TesisId == tesisId.Value);
        }

        var items = await query.Select(x => new { x.Durum, x.BeklenenValorTarihi, x.BrutTutar }).ToListAsync(cancellationToken);

        var ozet = new PosTahsilatValorOzetDto();
        foreach (var item in items)
        {
            if (item.Durum is PosTahsilatValorDurumlari.ValorBekliyor or PosTahsilatValorDurumlari.MutabakatBekliyor)
            {
                ozet.ValorBekleyenToplam += item.BrutTutar;
                ozet.ValorBekleyenAdet++;

                if (item.BeklenenValorTarihi == bugun)
                {
                    ozet.BugunValoruGelenToplam += item.BrutTutar;
                    ozet.BugunValoruGelenAdet++;
                }
                else if (item.BeklenenValorTarihi < bugun)
                {
                    ozet.ValoruGecmisToplam += item.BrutTutar;
                    ozet.ValoruGecmisAdet++;
                }
            }
            else if (item.Durum == PosTahsilatValorDurumlari.Aktarildi)
            {
                ozet.AktarilanToplam += item.BrutTutar;
                ozet.AktarilanAdet++;
            }
            else if (item.Durum == PosTahsilatValorDurumlari.Hata)
            {
                ozet.HataliAdet++;
            }
        }

        return ozet;
    }

    public async Task<PosTahsilatValorTopluOnayBilgisiDto> GetTopluOnayBilgisiAsync(PosTahsilatValorTopluOnayBilgisiRequest request, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.PosTahsilatValorleri.AsNoTracking().Where(x => !x.IsDeleted);

        if (request.ValorIdler is { Count: > 0 })
        {
            query = query.Where(x => request.ValorIdler.Contains(x.Id));
        }
        else
        {
            if (request.TesisId.HasValue)
            {
                query = query.Where(x => x.TesisId == request.TesisId.Value);
            }

            if (request.SadeceValoruGelenler)
            {
                var bugun = ValorTarihHesaplamaService.BugunIstanbul();
                query = query.Where(x => x.Durum == PosTahsilatValorDurumlari.ValorBekliyor && x.BeklenenValorTarihi <= bugun);
            }
        }

        var toplam = await query
            .GroupBy(x => 1)
            .Select(g => new
            {
                Adet = g.Count(),
                Brut = g.Sum(x => x.BrutTutar),
                Komisyon = g.Sum(x => x.KomisyonTutari),
                Net = g.Sum(x => x.NetTutar)
            })
            .FirstOrDefaultAsync(cancellationToken);

        return new PosTahsilatValorTopluOnayBilgisiDto
        {
            Adet = toplam?.Adet ?? 0,
            ToplamBrut = toplam?.Brut ?? 0,
            ToplamKomisyon = toplam?.Komisyon ?? 0,
            ToplamNet = toplam?.Net ?? 0
        };
    }

    private void Zenginlestir(PosTahsilatValorDto dto)
    {
        var durum = _valorTarihHesaplamaService.DegerlendirDurum(dto.BeklenenValorTarihi, dto.Durum);
        dto.ValoreKalanGun = durum.ValoreKalanGun;
        dto.ValorGectiMi = durum.ValorGectiMi;
        dto.BugunValorGunuMu = durum.BugunValorGunuMu;
        dto.AktarilabilirMi = durum.AktarilabilirMi;
    }

    private static Func<IQueryable<PosTahsilatValor>, IQueryable<PosTahsilatValor>> BuildIncludeQuery(
        Func<IQueryable<PosTahsilatValor>, IQueryable<PosTahsilatValor>>? include)
    {
        return query =>
        {
            var result = include is null ? query : include(query);
            return result
                .Include(x => x.TahsilatOdemeBelgesi)
                .Include(x => x.KrediKartiHesap)
                .Include(x => x.BagliBankaHesap);
        };
    }

    private static System.Linq.Expressions.Expression<Func<PosTahsilatValor, bool>>? BuildScopedPredicate(
        DomainAccessScope scope,
        System.Linq.Expressions.Expression<Func<PosTahsilatValor, bool>>? predicate)
    {
        if (!scope.IsScoped)
        {
            return predicate;
        }

        System.Linq.Expressions.Expression<Func<PosTahsilatValor, bool>> scopeFilter = x => scope.TesisIds.Contains(x.TesisId);
        if (predicate is null)
        {
            return scopeFilter;
        }

        var parameter = System.Linq.Expressions.Expression.Parameter(typeof(PosTahsilatValor));
        var body = System.Linq.Expressions.Expression.AndAlso(
            System.Linq.Expressions.Expression.Invoke(predicate, parameter),
            System.Linq.Expressions.Expression.Invoke(scopeFilter, parameter));
        return System.Linq.Expressions.Expression.Lambda<Func<PosTahsilatValor, bool>>(body, parameter);
    }
}
