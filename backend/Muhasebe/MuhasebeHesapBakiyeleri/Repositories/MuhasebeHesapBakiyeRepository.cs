using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.MuhasebeHesapBakiyeleri.Dtos;
using STYS.Muhasebe.MuhasebeHesapBakiyeleri.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Muhasebe.MuhasebeHesapBakiyeleri.Repositories;

public class MuhasebeHesapBakiyeRepository
    : BaseRdbmsRepository<MuhasebeHesapBakiye, int>,
      IMuhasebeHesapBakiyeRepository
{
    private readonly StysAppDbContext _dbContext;

    public MuhasebeHesapBakiyeRepository(StysAppDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
        _dbContext = dbContext;
    }

    public async Task<MuhasebeHesapBakiye?> GetByUniqueKeyAsync(
        int tesisId,
        int maliYil,
        int donem,
        int muhasebeHesapPlaniId,
        bool konsolideMi,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.MuhasebeHesapBakiyeleri
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.TesisId == tesisId
                && x.MaliYil == maliYil
                && x.Donem == donem
                && x.MuhasebeHesapPlaniId == muhasebeHesapPlaniId
                && x.KonsolideMi == konsolideMi
                && !x.IsDeleted,
                cancellationToken);
    }

    public async Task<List<MuhasebeHesapBakiye>> GetFilteredAsync(
        MuhasebeHesapBakiyeFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter(_dbContext.MuhasebeHesapBakiyeleri.AsNoTracking(), filter);

        query = query
            .OrderBy(x => x.TesisId)
            .ThenBy(x => x.MaliYil)
            .ThenBy(x => x.Donem)
            .ThenBy(x => x.HesapKodu)
            .ThenBy(x => x.KonsolideMi);

        return await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountFilteredAsync(
        MuhasebeHesapBakiyeFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter(_dbContext.MuhasebeHesapBakiyeleri.AsNoTracking(), filter);
        return await query.CountAsync(cancellationToken);
    }

    public async Task<List<MuhasebeHesapBakiye>> GetByTesisYilDonemAsync(
        int tesisId,
        int maliYil,
        int donem,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.MuhasebeHesapBakiyeleri
            .AsNoTracking()
            .Include(x => x.Tesis)
            .Include(x => x.MuhasebeHesapPlani)
            .Where(x =>
                x.TesisId == tesisId
                && x.MaliYil == maliYil
                && x.Donem == donem
                && !x.IsDeleted)
            .OrderBy(x => x.HesapKodu)
            .ThenBy(x => x.KonsolideMi)
            .ToListAsync(cancellationToken);
    }

    private static IQueryable<MuhasebeHesapBakiye> ApplyFilter(
        IQueryable<MuhasebeHesapBakiye> query,
        MuhasebeHesapBakiyeFilterDto filter)
    {
        query = query.Where(x => !x.IsDeleted);

        if (filter.TesisId.HasValue)
            query = query.Where(x => x.TesisId == filter.TesisId.Value);

        if (filter.MaliYil.HasValue)
            query = query.Where(x => x.MaliYil == filter.MaliYil.Value);

        if (filter.Donem.HasValue)
            query = query.Where(x => x.Donem == filter.Donem.Value);

        if (filter.MuhasebeHesapPlaniId.HasValue)
            query = query.Where(x => x.MuhasebeHesapPlaniId == filter.MuhasebeHesapPlaniId.Value);

        if (filter.KonsolideMi.HasValue)
            query = query.Where(x => x.KonsolideMi == filter.KonsolideMi.Value);

        if (!string.IsNullOrEmpty(filter.HesapKoduBaslangic))
            query = query.Where(x => string.Compare(x.HesapKodu, filter.HesapKoduBaslangic) >= 0);

        if (!string.IsNullOrEmpty(filter.HesapKoduBitis))
            query = query.Where(x => string.Compare(x.HesapKodu, filter.HesapKoduBitis) <= 0);

        return query;
    }
}
