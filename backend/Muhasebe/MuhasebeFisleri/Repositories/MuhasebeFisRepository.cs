using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.MuhasebeFisleri.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Muhasebe.MuhasebeFisleri.Repositories;

public class MuhasebeFisRepository
    : BaseRdbmsRepository<MuhasebeFis, int>,
      IMuhasebeFisRepository
{
    private readonly StysAppDbContext _dbContext;

    public MuhasebeFisRepository(StysAppDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
        _dbContext = dbContext;
    }

    public async Task<MuhasebeFis?> GetByIdWithSatirlarAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.MuhasebeFisler
            .Include(x => x.Satirlar.Where(s => !s.IsDeleted))
                .ThenInclude(s => s.MuhasebeHesapPlani)
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
    }

    public async Task<List<MuhasebeFis>> GetByKaynakAsync(string kaynakModul, int kaynakId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.MuhasebeFisler
            .Include(x => x.Satirlar.Where(s => !s.IsDeleted))
                .ThenInclude(s => s.MuhasebeHesapPlani)
            .Where(x => x.KaynakModul == kaynakModul && x.KaynakId == kaynakId && !x.IsDeleted)
            .OrderByDescending(x => x.FisTarihi)
            .ToListAsync(cancellationToken);
    }
}
