using Microsoft.EntityFrameworkCore;
using STYS.Agent.Services;
using STYS.Infrastructure.EntityFramework;
using STYS.Tests.TestSupport;
using TOD.Platform.Security.Auth.Services;
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

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    [IntegrationFact]
    public async Task CreateAgent_ShouldCreateAgentRecord()
    {
        var connectionString = Environment.GetEnvironmentVariable("STYS_INTEGRATION_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        _uniqueSuffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];

        await using var db = AgentTestSupport.CreateDbContext(connectionString);
        var (kurum, _, tesis) = await AgentTestSupport.SeedKurumIlTesisAsync(db, _uniqueSuffix);
        _kurumId = kurum.Id;
        _tesisId = tesis.Id;

        var factory = new DbContextFactoryForTest<StysAppDbContext>(db);
        var service = new AgentService(factory, new FakeSuperAdminTenantAccessor());

        var request = new STYS.Agent.Contracts.Dtos.AgentKaydetRequest
        {
            Ad = $"Test-Agent-{_uniqueSuffix}", KurumId = _kurumId, TesisIds = [_tesisId], Scopes = ["agent.heartbeat"]
        };

        var result = await service.CreateAsync(request, "test-user", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(request.Ad, result.Ad);
        Assert.Equal(_kurumId, result.KurumId);
        Assert.Contains(_tesisId, result.TesisIds);
        Assert.Contains("agent.heartbeat", result.Scopes);

        await AgentTestSupport.CleanupAsync(db, _uniqueSuffix);
    }

    [IntegrationFact]
    public async Task GetAgent_ShouldReturnAgentWithTesis()
    {
        var connectionString = Environment.GetEnvironmentVariable("STYS_INTEGRATION_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        _uniqueSuffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];

        await using var db = AgentTestSupport.CreateDbContext(connectionString);
        var (kurum, _, _) = await AgentTestSupport.SeedKurumIlTesisAsync(db, _uniqueSuffix);
        _kurumId = kurum.Id;

        var agent = await AgentTestSupport.SeedAgentAsync(db, _kurumId, _uniqueSuffix);

        var factory = new DbContextFactoryForTest<StysAppDbContext>(db);
        var service = new AgentService(factory, new FakeSuperAdminTenantAccessor());

        var result = await service.GetByIdAsync(agent.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(agent.Ad, result.Ad);

        await AgentTestSupport.CleanupAsync(db, _uniqueSuffix);
    }

    [IntegrationFact]
    public async Task KurumA_Admin_CannotAccessKurumB_Agent()
    {
        var connectionString = Environment.GetEnvironmentVariable("STYS_INTEGRATION_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        _uniqueSuffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];

        await using var db = AgentTestSupport.CreateDbContext(connectionString);
        var (kurumA, _, _) = await AgentTestSupport.SeedKurumIlTesisAsync(db, $"{_uniqueSuffix}-A");
        var (kurumB, _, _) = await AgentTestSupport.SeedKurumIlTesisAsync(db, $"{_uniqueSuffix}-B");

        var agentA = await AgentTestSupport.SeedAgentAsync(db, kurumA.Id, $"{_uniqueSuffix}-A");
        var agentB = await AgentTestSupport.SeedAgentAsync(db, kurumB.Id, $"{_uniqueSuffix}-B");

        var factory = new DbContextFactoryForTest<StysAppDbContext>(db);

        var serviceA = new AgentService(factory, new FakeKurumTenantAccessor(kurumA.Id));
        var result = await serviceA.GetByIdAsync(agentA.Id, CancellationToken.None);
        Assert.NotNull(result);

        var accessorB = new FakeKurumTenantAccessor(kurumB.Id);
        var serviceB = new AgentService(factory, accessorB);
        var ex = await Assert.ThrowsAsync<TOD.Platform.SharedKernel.Exceptions.BaseException>(() => serviceB.GetByIdAsync(agentA.Id, CancellationToken.None));
        Assert.Equal(403, ex.ErrorCode);

        var allA = await serviceA.GetAllAsync(CancellationToken.None);
        Assert.All(allA, x => Assert.Equal(kurumA.Id, x.KurumId));

        await AgentTestSupport.CleanupAsync(db, $"{_uniqueSuffix}-B");
        await AgentTestSupport.CleanupAsync(db, $"{_uniqueSuffix}-A");
    }

    [IntegrationFact]
    public async Task InvalidTesis_ShouldBeRejected()
    {
        var connectionString = Environment.GetEnvironmentVariable("STYS_INTEGRATION_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        _uniqueSuffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];

        await using var db = AgentTestSupport.CreateDbContext(connectionString);
        var (kurumA, _, tesisA) = await AgentTestSupport.SeedKurumIlTesisAsync(db, $"{_uniqueSuffix}-A");
        var (kurumB, _, tesisB) = await AgentTestSupport.SeedKurumIlTesisAsync(db, $"{_uniqueSuffix}-B");

        var factory = new DbContextFactoryForTest<StysAppDbContext>(db);
        var service = new AgentService(factory, new FakeSuperAdminTenantAccessor());

        var request = new STYS.Agent.Contracts.Dtos.AgentKaydetRequest
        {
            Ad = "Test", KurumId = kurumA.Id, TesisIds = [tesisB.Id], Scopes = ["agent.heartbeat"]
        };

        var ex = await Assert.ThrowsAsync<TOD.Platform.SharedKernel.Exceptions.BaseException>(() => service.CreateAsync(request, "test", CancellationToken.None));
        Assert.Equal(400, ex.ErrorCode);

        await AgentTestSupport.CleanupAsync(db, $"{_uniqueSuffix}-B");
        await AgentTestSupport.CleanupAsync(db, $"{_uniqueSuffix}-A");
    }
}
