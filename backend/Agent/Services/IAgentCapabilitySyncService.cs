using STYS.Agent.Entities;
using STYS.Infrastructure.EntityFramework;
using AgentEntity = STYS.Agent.Entities.Agent;

namespace STYS.Agent.Services;

public interface IAgentCapabilitySyncService
{
    void SyncFromHeartbeat(StysAppDbContext db, AgentEntity agent, IReadOnlyCollection<string> supportedCapabilities);
}
