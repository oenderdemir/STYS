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
}
