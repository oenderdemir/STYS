using Microsoft.EntityFrameworkCore;
using STYS.Agent.Entities;
using STYS.Agent.Services;
using STYS.Infrastructure.EntityFramework;
using STYS.Tests.TestSupport;
using Xunit;
using AgentEntity = STYS.Agent.Entities.Agent;

namespace STYS.Tests.Agent;

public sealed class AgentCapabilitySyncServiceTests
{
    [Fact]
    public async Task Heartbeat_SyncsPavoCapability_AndKeepsCapabilitiesActive()
    {
        var dbName = $"stys-capability-sync-{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<StysAppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        await using var db = new StysAppDbContext(options, currentTenantAccessor: new SuperTenantAccessor());

        var agent = new AgentEntity
        {
            Ad = "Agent-1",
            AgentKey = "AGENT-1",
            KurumId = 1000,
            Durum = STYS.Agent.Contracts.Enums.AgentDurum.Active,
            CreatedBy = "test",
            CreatedAt = DateTime.UtcNow
        };

        db.Set<AgentEntity>().Add(agent);
        await db.SaveChangesAsync();

        db.Set<AgentCapability>().Add(new AgentCapability
        {
            AgentId = agent.Id,
            KurumId = agent.KurumId,
            Capability = "config-read",
            AktifMi = false,
            CreatedBy = "seed",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = new AgentCapabilitySyncService();
        service.SyncFromHeartbeat(db, agent, ["heartbeat", "config-read", "pavo"]);
        await db.SaveChangesAsync();

        var capabilities = await db.Set<AgentCapability>()
            .AsNoTracking()
            .Where(x => x.AgentId == agent.Id && !x.IsDeleted)
            .ToListAsync();

        Assert.Contains(capabilities, x => x.Capability == "pavo" && x.AktifMi);
        Assert.Contains(capabilities, x => x.Capability == "heartbeat" && x.AktifMi);
        Assert.Contains(capabilities, x => x.Capability == "config-read" && x.AktifMi);
    }
}
