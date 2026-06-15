using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Kurumlar.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Kurumlar.Repositories;

public class KurumRepository : BaseRdbmsRepository<Kurum, int>, IKurumRepository
{
    public KurumRepository(StysAppDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
    }

    public Task<bool> ExistsByKodAsync(string kod, int? excludedId = null, CancellationToken cancellationToken = default)
    {
        var normalizedKod = kod.Trim().ToUpperInvariant();
        return DbSet.AnyAsync(
            x => x.Kod.ToUpper() == normalizedKod && (!excludedId.HasValue || x.Id != excludedId.Value),
            cancellationToken);
    }
}
