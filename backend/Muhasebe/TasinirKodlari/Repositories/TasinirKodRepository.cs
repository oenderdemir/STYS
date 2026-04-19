using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.TasinirKodlari.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Muhasebe.TasinirKodlari.Repositories;

public class TasinirKodRepository : BaseRdbmsRepository<TasinirKod, int>, ITasinirKodRepository
{
    private readonly StysAppDbContext _dbContext;

    public TasinirKodRepository(StysAppDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
        _dbContext = dbContext;
    }

    public async Task<List<TasinirKod>> GetByTamKodlarAsync(IEnumerable<string> tamKodlar, CancellationToken cancellationToken = default)
    {
        var kodlar = tamKodlar
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (kodlar.Count == 0)
        {
            return [];
        }

        return await _dbContext.TasinirKodlar
            .Where(x => kodlar.Contains(x.TamKod))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<TasinirKod>> GetRootNodesAsync(CancellationToken cancellationToken = default)
        => await _dbContext.TasinirKodlar
            .Where(x => x.UstKodId == null)
            .OrderBy(x => x.TamKod)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

    public async Task<List<TasinirKod>> GetChildrenByParentIdAsync(int parentId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.TasinirKodlar
            .Where(x => x.UstKodId == parentId)
            .OrderBy(x => x.TamKod)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> HasChildrenAsync(int parentId, CancellationToken cancellationToken = default)
        => await _dbContext.TasinirKodlar.AnyAsync(x => x.UstKodId == parentId, cancellationToken);
}
