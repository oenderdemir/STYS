using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.RestoranMasalari.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.RestoranMasalari.Repositories;

public class RestoranMasaRepository : BaseRdbmsRepository<RestoranMasa, int>, IRestoranMasaRepository
{
    private readonly StysAppDbContext _dbContext;

    public RestoranMasaRepository(StysAppDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
        _dbContext = dbContext;
    }

    public Task<List<RestoranMasa>> GetByRestoranIdAsync(int restoranId, CancellationToken cancellationToken = default)
        => _dbContext.RestoranMasalari
            .Where(x => x.RestoranId == restoranId)
            .OrderBy(x => x.MasaNo)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
}
