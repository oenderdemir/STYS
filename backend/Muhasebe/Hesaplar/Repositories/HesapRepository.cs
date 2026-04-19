using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.Hesaplar.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Muhasebe.Hesaplar.Repositories;

public class HesapRepository : BaseRdbmsRepository<Hesap, int>, IHesapRepository
{
    private readonly StysAppDbContext _dbContext;

    public HesapRepository(StysAppDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
        _dbContext = dbContext;
    }

    public async Task<Hesap?> GetDetailByIdAsync(int id, CancellationToken cancellationToken = default)
        => await _dbContext.Set<Hesap>()
            .Include(x => x.MuhasebeHesapPlani)
            .Include(x => x.KasaBankaBaglantilari).ThenInclude(x => x.KasaBankaHesap)
            .Include(x => x.DepoBaglantilari).ThenInclude(x => x.Depo)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<List<Hesap>> GetAllWithDetailsAsync(CancellationToken cancellationToken = default)
        => await _dbContext.Set<Hesap>()
            .AsNoTracking()
            .Include(x => x.MuhasebeHesapPlani)
            .Include(x => x.KasaBankaBaglantilari).ThenInclude(x => x.KasaBankaHesap)
            .Include(x => x.DepoBaglantilari).ThenInclude(x => x.Depo)
            .OrderBy(x => x.Ad)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

    public async Task<bool> ExistsByAdAsync(string ad, int? excludeId = null, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Set<Hesap>().AsNoTracking().Where(x => x.Ad == ad);
        if (excludeId.HasValue)
        {
            query = query.Where(x => x.Id != excludeId.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }
}
