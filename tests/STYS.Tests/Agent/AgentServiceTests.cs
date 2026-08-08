using Microsoft.EntityFrameworkCore;
using STYS.Agent.Entities;
using STYS.Agent.Services;
using STYS.Infrastructure.EntityFramework;
using STYS.Tests.TestSupport;
using Xunit;

namespace STYS.Tests.Agent;

[Collection(SqlServerIntegrationCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Domain", "Agent")]
[Trait("TestLevel", "SqlIntegration")]
[Trait("Dependency", "SqlServer")]
public sealed class AgentServiceTests : IAsyncLifetime
{
    private const string TestMarker = "agentsvc";
    private string _uniqueSuffix = string.Empty;
    private int _kurumId;
    private int _tesisId;

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [IntegrationFact]
    public async Task CreateAgent_ShouldCreateAgentRecord()
    {
        var connectionString = Environment.GetEnvironmentVariable("STYS_INTEGRATION_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        _uniqueSuffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];

        await using var db = AgentTestSupport.CreateDbContext(connectionString);
        var (kurum, _, tesis) = await AgentTestSupport.SeedKurumIlTesisAsync(db, _uniqueSuffix);
        _kurumId = kurum.Id;
        _tesisId = tesis.Id;

        var factory = new DbContextFactoryForTest<StysAppDbContext>(db);
        var service = new AgentService(factory);

        var request = new STYS.Agent.Contracts.Dtos.AgentKaydetRequest
        {
            Ad = $"Test-Agent-{_uniqueSuffix}",
            KurumId = _kurumId,
            TesisIds = [_tesisId],
            Scopes = ["Agent.Heartbeat"]
        };

        var result = await service.CreateAsync(request, "test-user", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(request.Ad, result.Ad);
        Assert.Equal(_kurumId, result.KurumId);
        Assert.Contains(_tesisId, result.TesisIds);

        await AgentTestSupport.CleanupAsync(db, _uniqueSuffix);
    }

    [IntegrationFact]
    public async Task GetAgent_ShouldReturnAgentWithTesis()
    {
        var connectionString = Environment.GetEnvironmentVariable("STYS_INTEGRATION_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        _uniqueSuffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];

        await using var db = AgentTestSupport.CreateDbContext(connectionString);
        var (kurum, _, tesis) = await AgentTestSupport.SeedKurumIlTesisAsync(db, _uniqueSuffix);
        _kurumId = kurum.Id;

        var agent = await AgentTestSupport.SeedAgentAsync(db, _kurumId, _uniqueSuffix);

        var factory = new DbContextFactoryForTest<StysAppDbContext>(db);
        var service = new AgentService(factory);

        var result = await service.GetByIdAsync(agent.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(agent.Ad, result.Ad);

        await AgentTestSupport.CleanupAsync(db, _uniqueSuffix);
    }
}

internal sealed class DbContextFactoryForTest<TContext> : IDbContextFactory<TContext> where TContext : DbContext
{
    private readonly TContext _context;

    public DbContextFactoryForTest(TContext context)
    {
        _context = context;
    }

    public TContext CreateDbContext()
    {
        return _context;
    }

    public ValueTask<TContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        return new ValueTask<TContext>(_context);
    }
}
