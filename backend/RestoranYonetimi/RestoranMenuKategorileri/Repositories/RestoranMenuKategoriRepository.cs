using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.RestoranMenuKategorileri.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.RestoranMenuKategorileri.Repositories;

public class RestoranMenuKategoriRepository : BaseRdbmsRepository<RestoranMenuKategori, int>, IRestoranMenuKategoriRepository
{
    private readonly StysAppDbContext _dbContext;

    public RestoranMenuKategoriRepository(StysAppDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
        _dbContext = dbContext;
    }

    public Task<List<RestoranMenuKategori>> GetByRestoranIdWithUrunlerAsync(int restoranId, CancellationToken cancellationToken = default)
        => _dbContext.RestoranMenuKategorileri
            .Include(x => x.Urunler)
            .Where(x => x.RestoranId == restoranId)
            .OrderBy(x => x.SiraNo)
            .ThenBy(x => x.Ad)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
}
