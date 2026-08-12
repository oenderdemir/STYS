using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using STYS.Agent.Contracts.Dtos;
using STYS.Agent.Contracts.Enums;
using STYS.Agent.Entities;
using STYS.Agent.Options;
using STYS.Agent.Services;
using STYS.Infrastructure.EntityFramework;
using STYS.Tests.TestSupport;
using TOD.Platform.SharedKernel.Exceptions;
using Xunit;
using AgentEntity = STYS.Agent.Entities.Agent;

namespace STYS.Tests.Agent;

[Collection(SqlServerIntegrationCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Domain", "Agent")]
[Trait("TestLevel", "SqlIntegration")]
public sealed class AgentCompatibilityPolicyIntegrationTests : IAsyncLifetime
{
    private const string TestMarker = "agentcompat";
    private string _connectionString = string.Empty;
    private string _suffix = string.Empty;
    private int _kurumId;
    private int _tesisId;

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    [IntegrationFact]
    public async Task UpdateRequired_StartPaymentBlocked_ButGetPaymentResultAllowed()
    {
        await using var db = await SetupAsync();
        if (db is null) return;

        var agent = await SeedVersionedAgentAsync(db, "1.0.0", "1.0.0");
        var svc = CreateCommandService(compatibilityOptions: new AgentCompatibilityOptions
        {
            MinimumSupportedAgentVersion = "2.0.0",
            RecommendedAgentVersion = "3.0.0",
            SupportedContractVersion = "1.0.0"
        });

        await Assert.ThrowsAsync<BaseException>(() => svc.SendAsync(new AgentCommandSendRequest
        {
            AgentId = agent.Id,
            CommandType = "PavoStartPayment",
            Priority = 1
        }, "test", CancellationToken.None));

        var startCommandCount = await db.Set<AgentCommand>()
            .CountAsync(x => x.AgentId == agent.Id && x.CommandType == "PavoStartPayment");
        Assert.Equal(0, startCommandCount);

        var resultCommand = await svc.SendAsync(new AgentCommandSendRequest
        {
            AgentId = agent.Id,
            CommandType = "PavoGetPaymentResult",
            Priority = 1
        }, "test", CancellationToken.None);

        Assert.Equal("PavoGetPaymentResult", resultCommand.CommandType);
        Assert.Equal(agent.Id, resultCommand.AgentId);

        await CleanupAsync(db);
    }

    [IntegrationFact]
    public async Task IncompatibleContract_StartPaymentBlocked()
    {
        await using var db = await SetupAsync();
        if (db is null) return;

        var agent = await SeedVersionedAgentAsync(db, "3.0.0", "2.0.0");
        var svc = CreateCommandService(compatibilityOptions: new AgentCompatibilityOptions
        {
            MinimumSupportedAgentVersion = "1.0.0",
            RecommendedAgentVersion = "2.0.0",
            SupportedContractVersion = "1.0.0"
        });

        await Assert.ThrowsAsync<BaseException>(() => svc.SendAsync(new AgentCommandSendRequest
        {
            AgentId = agent.Id,
            CommandType = "PavoStartPayment",
            Priority = 1
        }, "test", CancellationToken.None));

        var commandCount = await db.Set<AgentCommand>()
            .CountAsync(x => x.AgentId == agent.Id && x.CommandType == "PavoStartPayment");
        Assert.Equal(0, commandCount);

        await CleanupAsync(db);
    }

    [IntegrationFact]
    public async Task TenantIsolation_ShouldRemainAuthoritative()
    {
        await using var db = await SetupAsync();
        if (db is null) return;

        var otherKurum = await AgentTestSupport.SeedKurumIlTesisAsync(db, $"{_suffix}-other");
        var otherAgent = await AgentTestSupport.SeedAgentAsync(db, otherKurum.kurum.Id, $"{_suffix}-other-agent");
        await AttachAgentToTesisAsync(db, otherAgent.Id, otherKurum.kurum.Id, otherKurum.tesis.Id);
        await AddScopeAndCapabilityAsync(db, otherAgent.Id, otherKurum.kurum.Id);

        var svc = new AgentCommandService(
            new DbContextFactoryForTest<StysAppDbContext>(() => AgentTestSupport.CreateDbContext(_connectionString)),
            new FakeKurumTenantAccessor(_kurumId),
            NullLogger<AgentCommandService>.Instance,
            compatibilityOptions: Options.Create(new AgentCompatibilityOptions()));

        await Assert.ThrowsAsync<BaseException>(() => svc.SendAsync(new AgentCommandSendRequest
        {
            AgentId = otherAgent.Id,
            CommandType = "Ping",
            Priority = 1
        }, "test", CancellationToken.None));

        await CleanupAsync(db);
        await AgentTestSupport.CleanupAsync(db, $"{_suffix}-other");
    }

    [IntegrationFact]
    public async Task AgentDetail_ShouldExposeCompatibilityFields()
    {
        await using var db = await SetupAsync();
        if (db is null) return;

        var agent = await SeedVersionedAgentAsync(db, "1.5.0", "1.0.0");
        var service = new AgentService(
            new DbContextFactoryForTest<StysAppDbContext>(() => AgentTestSupport.CreateDbContext(_connectionString)),
            new FakeSuperAdminTenantAccessor(),
            Options.Create(new AgentCompatibilityOptions
            {
                MinimumSupportedAgentVersion = "1.0.0",
                RecommendedAgentVersion = "2.0.0",
                SupportedContractVersion = "1.0.0"
            }));

        var dto = await service.GetByIdAsync(agent.Id, CancellationToken.None);

        Assert.Equal("1.5.0", dto.AgentVersion);
        Assert.Equal("1.0.0", dto.ContractVersion);
        Assert.Equal(AgentCompatibilityStatus.UpdateAvailable, dto.CompatibilityStatus);
        Assert.Equal("1.0.0", dto.MinimumSupportedAgentVersion);
        Assert.Equal("2.0.0", dto.RecommendedAgentVersion);
        Assert.Equal("1.0.0", dto.SupportedContractVersion);

        await CleanupAsync(db);
    }

    private async Task<StysAppDbContext> SetupAsync()
    {
        _connectionString = Environment.GetEnvironmentVariable(IntegrationFactAttribute.ConnectionStringEnvVar);
        if (string.IsNullOrWhiteSpace(_connectionString)) return null!;
        _suffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];

        var db = AgentTestSupport.CreateDbContext(_connectionString);
        var (kurum, _, tesis) = await AgentTestSupport.SeedKurumIlTesisAsync(db, _suffix);
        _kurumId = kurum.Id;
        _tesisId = tesis.Id;
        return db;
    }

    private async Task<AgentEntity> SeedVersionedAgentAsync(StysAppDbContext db, string agentVersion, string contractVersion)
    {
        var agent = await AgentTestSupport.SeedAgentAsync(db, _kurumId, _suffix);
        agent.AgentVersion = agentVersion;
        agent.ContractVersion = contractVersion;
        await AddScopeAndCapabilityAsync(db, agent.Id, agent.KurumId);
        return agent;
    }

    private static async Task AddScopeAndCapabilityAsync(StysAppDbContext db, int agentId, int kurumId)
    {
        if (!await db.Set<AgentScope>().AnyAsync(x => x.AgentId == agentId && x.Scope == "agent.command.execute" && !x.IsDeleted))
        {
            db.Set<AgentScope>().Add(new AgentScope
            {
                AgentId = agentId,
                KurumId = kurumId,
                Scope = "agent.command.execute",
                AktifMi = true,
                CreatedBy = "test",
                CreatedAt = DateTime.UtcNow
            });
        }

        if (!await db.Set<AgentCapability>().AnyAsync(x => x.AgentId == agentId && x.Capability == "pavo" && !x.IsDeleted))
        {
            db.Set<AgentCapability>().Add(new AgentCapability
            {
                AgentId = agentId,
                KurumId = kurumId,
                Capability = "pavo",
                AktifMi = true,
                CreatedBy = "test",
                CreatedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync();
    }

    private static async Task AttachAgentToTesisAsync(StysAppDbContext db, int agentId, int kurumId, int tesisId)
    {
        if (!await db.Set<AgentTesis>().AnyAsync(x => x.AgentId == agentId && x.TesisId == tesisId && !x.IsDeleted))
        {
            db.Set<AgentTesis>().Add(new AgentTesis
            {
                AgentId = agentId,
                KurumId = kurumId,
                TesisId = tesisId,
                CreatedBy = "test",
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
    }

    private AgentCommandService CreateCommandService(AgentCompatibilityOptions? compatibilityOptions = null) =>
        new(
            new DbContextFactoryForTest<StysAppDbContext>(() => AgentTestSupport.CreateDbContext(_connectionString)),
            new FakeKurumTenantAccessor(_kurumId),
            NullLogger<AgentCommandService>.Instance,
            compatibilityOptions: compatibilityOptions is null ? null : Options.Create(compatibilityOptions));

    private async Task CleanupAsync(StysAppDbContext db)
    {
        await AgentTestSupport.CleanupAsync(db, _suffix);
    }
}
