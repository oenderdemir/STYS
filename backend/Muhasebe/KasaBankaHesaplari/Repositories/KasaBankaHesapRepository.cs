using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.KasaBankaHesaplari.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Muhasebe.KasaBankaHesaplari.Repositories;

public class KasaBankaHesapRepository : BaseRdbmsRepository<KasaBankaHesap, int>, IKasaBankaHesapRepository
{
    private readonly StysAppDbContext _dbContext;

    public KasaBankaHesapRepository(StysAppDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
        _dbContext = dbContext;
    }

    public async Task<List<KasaBankaHesap>> GetByTipAsync(string tip, bool onlyActive, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Set<KasaBankaHesap>()
            .AsNoTracking()
            .Include(x => x.MuhasebeHesapPlani)
            .Where(x => x.Tip == tip);

        if (onlyActive)
        {
            query = query.Where(x => x.AktifMi);
        }

        return await query
            .OrderBy(x => x.Kod)
            .ThenBy(x => x.Ad)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsByKodAsync(string kod, int? excludeId = null, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Set<KasaBankaHesap>()
            .AsNoTracking()
            .Where(x => x.Kod == kod);

        if (excludeId.HasValue)
        {
            query = query.Where(x => x.Id != excludeId.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }
}
