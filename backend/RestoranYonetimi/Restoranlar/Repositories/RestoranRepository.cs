using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Restoranlar.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Restoranlar.Repositories;

public class RestoranRepository : BaseRdbmsRepository<Restoran, int>, IRestoranRepository
{
    private readonly StysAppDbContext _dbContext;

    public RestoranRepository(StysAppDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
        _dbContext = dbContext;
    }

    public Task<List<Restoran>> GetByTesisIdAsync(int tesisId, CancellationToken cancellationToken = default)
        => _dbContext.Restoranlar
            .Where(x => x.TesisId == tesisId)
            .OrderBy(x => x.Ad)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
}
