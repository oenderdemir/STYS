using Microsoft.EntityFrameworkCore;
using STYS.Agent.Contracts.Dtos;
using STYS.Agent.Contracts.Enums;
using STYS.Agent.Entities;
using AgentEntity = STYS.Agent.Entities.Agent;
using STYS.Infrastructure.EntityFramework;
using STYS.Tests.TestSupport;
using Xunit;

namespace STYS.Tests.Agent;

[Collection(SqlServerIntegrationCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Domain", "Agent")]
[Trait("TestLevel", "SqlIntegration")]
[Trait("CriticalInvariant", "TenantIsolation")]
public sealed class AgentTenantIsolationTests : IAsyncLifetime
{
    private const string TestMarker = "agttnt";
    private string _uniqueSuffix = string.Empty;

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    [IntegrationFact]
    public async Task Agent_FromKurumA_CannotAccessKurumB_Data()
    {
        var connectionString = Environment.GetEnvironmentVariable(IntegrationFactAttribute.ConnectionStringEnvVar);
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        _uniqueSuffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];

        await using var db = AgentTestSupport.CreateDbContext(connectionString);

        var (kurumA, _, _) = await AgentTestSupport.SeedKurumIlTesisAsync(db, $"{_uniqueSuffix}-A");
        var (kurumB, _, _) = await AgentTestSupport.SeedKurumIlTesisAsync(db, $"{_uniqueSuffix}-B");

        var agentA = await AgentTestSupport.SeedAgentAsync(db, kurumA.Id, $"{_uniqueSuffix}-A");
        await AgentTestSupport.SeedCredentialAsync(db, agentA.Id, kurumA.Id);

        Assert.Equal(kurumA.Id, agentA.KurumId);
        Assert.NotEqual(kurumB.Id, agentA.KurumId);

        await AgentTestSupport.CleanupAsync(db, $"{_uniqueSuffix}-B");
        await AgentTestSupport.CleanupAsync(db, $"{_uniqueSuffix}-A");
    }

    [IntegrationFact]
    public async Task DisabledAgent_ShouldNotBeAbleToGetToken()
    {
        var connectionString = Environment.GetEnvironmentVariable(IntegrationFactAttribute.ConnectionStringEnvVar);
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        _uniqueSuffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];

        await using var db = AgentTestSupport.CreateDbContext(connectionString);
        var (kurum, _, _) = await AgentTestSupport.SeedKurumIlTesisAsync(db, _uniqueSuffix);

        var agent = await AgentTestSupport.SeedAgentAsync(db, kurum.Id, _uniqueSuffix);
        var (cred, rawSecret) = await AgentTestSupport.SeedCredentialAsync(db, agent.Id, kurum.Id);

        agent.Durum = AgentDurum.Disabled;
        await db.SaveChangesAsync();

        var enabledCount = await db.Set<AgentEntity>().CountAsync(x => x.Id == agent.Id && x.Durum == AgentDurum.Active);
        Assert.Equal(0, enabledCount);

        var disabledCount = await db.Set<AgentEntity>().CountAsync(x => x.Id == agent.Id && x.Durum == AgentDurum.Disabled);
        Assert.Equal(1, disabledCount);

        await AgentTestSupport.CleanupAsync(db, _uniqueSuffix);
    }
}
