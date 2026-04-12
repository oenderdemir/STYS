using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.RestoranMenuUrunleri.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.RestoranMenuUrunleri.Repositories;

public class RestoranMenuUrunRepository : BaseRdbmsRepository<RestoranMenuUrun, int>, IRestoranMenuUrunRepository
{
    private readonly StysAppDbContext _dbContext;

    public RestoranMenuUrunRepository(StysAppDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
        _dbContext = dbContext;
    }

    public Task<List<RestoranMenuUrun>> GetByKategoriIdAsync(int kategoriId, CancellationToken cancellationToken = default)
        => _dbContext.RestoranMenuUrunleri
            .Where(x => x.RestoranMenuKategoriId == kategoriId)
            .OrderBy(x => x.Ad)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
}
