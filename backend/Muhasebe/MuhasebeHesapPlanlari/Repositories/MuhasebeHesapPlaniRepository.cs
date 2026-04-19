using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Muhasebe.MuhasebeHesapPlanlari.Repositories;

public class MuhasebeHesapPlaniRepository : BaseRdbmsRepository<MuhasebeHesapPlani, int>, IMuhasebeHesapPlaniRepository
{
    private readonly StysAppDbContext _dbContext;

    public MuhasebeHesapPlaniRepository(StysAppDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
        _dbContext = dbContext;
    }

    public async Task<List<MuhasebeHesapPlani>> GetRootNodesAsync(CancellationToken cancellationToken = default)
        => await _dbContext.MuhasebeHesapPlanlari
            .Where(x => x.SeviyeNo == 1)
            .OrderBy(x => x.TamKod)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

    public async Task<List<MuhasebeHesapPlani>> GetChildrenByParentIdAsync(int parentId, CancellationToken cancellationToken = default)
    {
        var parent = await _dbContext.MuhasebeHesapPlanlari.FirstOrDefaultAsync(x => x.Id == parentId, cancellationToken);
        if (parent is null)
        {
            return [];
        }

        var prefix = $"{parent.TamKod}.";
        var childLevel = parent.SeviyeNo + 1;
        return await _dbContext.MuhasebeHesapPlanlari
            .Where(x => x.SeviyeNo == childLevel && x.TamKod.StartsWith(prefix))
            .OrderBy(x => x.TamKod)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> HasChildrenAsync(string parentTamKod, int parentLevel, CancellationToken cancellationToken = default)
    {
        var prefix = $"{parentTamKod}.";
        var childLevel = parentLevel + 1;
        return await _dbContext.MuhasebeHesapPlanlari.AnyAsync(x => x.SeviyeNo == childLevel && x.TamKod.StartsWith(prefix), cancellationToken);
    }

    public async Task<List<MuhasebeHesapPlani>> GetByTamKodPrefixAsync(string tamKodPrefix, CancellationToken cancellationToken = default)
        => await _dbContext.MuhasebeHesapPlanlari
            .Where(x => x.AktifMi && x.TamKod.StartsWith(tamKodPrefix))
            .OrderBy(x => x.TamKod)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
}
