using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using TOD.Platform.Security.Auth.Services;

namespace STYS.Kurumlar.Services;

public class KurumLookupService : IKurumLookupService
{
    private readonly StysAppDbContext _dbContext;

    public KurumLookupService(StysAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> IsActiveKurumAsync(int kurumId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Kurumlar.AnyAsync(x => x.Id == kurumId && x.AktifMi, cancellationToken);
    }
}