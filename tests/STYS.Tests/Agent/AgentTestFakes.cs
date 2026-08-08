using Microsoft.EntityFrameworkCore;
using TOD.Platform.Security.Auth.Services;

namespace STYS.Tests.Agent;

internal sealed class FakeSuperAdminTenantAccessor : ICurrentTenantAccessor
{
    public int? GetCurrentKurumId() => null;
    public IReadOnlyList<int> GetAccessibleKurumIds() => new List<int>();
    public bool IsSuperAdmin() => true;
    public bool IsKurumAdmin() => false;
}

internal sealed class FakeKurumTenantAccessor : ICurrentTenantAccessor
{
    private int _kurumId;
    public FakeKurumTenantAccessor(int kurumId) => _kurumId = kurumId;
    public void SetKurumId(int kurumId) => _kurumId = kurumId;
    public int? GetCurrentKurumId() => _kurumId;
    public IReadOnlyList<int> GetAccessibleKurumIds() => new List<int> { _kurumId };
    public bool IsSuperAdmin() => false;
    public bool IsKurumAdmin() => true;
}

internal sealed class DbContextFactoryForTest<TContext> : IDbContextFactory<TContext> where TContext : DbContext
{
    private readonly TContext _context;
    public DbContextFactoryForTest(TContext context) => _context = context;
    public TContext CreateDbContext() => _context;
    public ValueTask<TContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => new(_context);
}
