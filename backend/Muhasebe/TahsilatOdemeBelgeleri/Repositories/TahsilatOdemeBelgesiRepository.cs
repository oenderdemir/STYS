using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Muhasebe.TahsilatOdemeBelgeleri.Repositories;

public class TahsilatOdemeBelgesiRepository : BaseRdbmsRepository<TahsilatOdemeBelgesi, int>, ITahsilatOdemeBelgesiRepository
{
    private readonly StysAppDbContext _dbContext;

    public TahsilatOdemeBelgesiRepository(StysAppDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
        _dbContext = dbContext;
    }

    public async Task<List<TahsilatOdemeBelgesi>> GetGunlukAsync(DateTime gun, CancellationToken cancellationToken = default)
    {
        var baslangic = gun.Date;
        var bitis = baslangic.AddDays(1);
        return await _dbContext.TahsilatOdemeBelgeleri
            .Where(x => x.BelgeTarihi >= baslangic && x.BelgeTarihi < bitis)
            .ToListAsync(cancellationToken);
    }
}

