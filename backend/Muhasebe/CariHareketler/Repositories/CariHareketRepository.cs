using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.CariHareketler.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Muhasebe.CariHareketler.Repositories;

public class CariHareketRepository : BaseRdbmsRepository<CariHareket, int>, ICariHareketRepository
{
    private readonly StysAppDbContext _dbContext;

    public CariHareketRepository(StysAppDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
        _dbContext = dbContext;
    }

    public async Task<List<CariHareket>> GetCariEkstresiAsync(int cariKartId, DateTime? baslangic, DateTime? bitis, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.CariHareketler.Where(x => x.CariKartId == cariKartId);
        if (baslangic.HasValue)
        {
            query = query.Where(x => x.HareketTarihi >= baslangic.Value.Date);
        }

        if (bitis.HasValue)
        {
            var bitisDate = bitis.Value.Date.AddDays(1);
            query = query.Where(x => x.HareketTarihi < bitisDate);
        }

        return await query
            .OrderBy(x => x.HareketTarihi)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
    }
}

