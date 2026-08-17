using Microsoft.EntityFrameworkCore;
using STYS.Agent.Entities;
using STYS.Infrastructure.EntityFramework;
using AgentEntity = STYS.Agent.Entities.Agent;

namespace STYS.Agent.Services;

public sealed class AgentCapabilitySyncService : IAgentCapabilitySyncService
{
    public void SyncFromHeartbeat(StysAppDbContext db, AgentEntity agent, IReadOnlyCollection<string> supportedCapabilities)
    {
        if (db is null)
        {
            throw new ArgumentNullException(nameof(db));
        }

        if (agent is null || agent.IsDeleted)
        {
            return;
        }

        var normalizedIncoming = (supportedCapabilities ?? Array.Empty<string>())
            .Select(NormalizeCapability)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var now = DateTime.UtcNow;
        var existing = db.Set<AgentCapability>()
            .Where(x => x.AgentId == agent.Id && !x.IsDeleted)
            .ToList();

        var incomingSet = normalizedIncoming.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingSet = existing
            .Select(x => x.Capability)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var capability in existing)
        {
            if (incomingSet.Contains(capability.Capability))
            {
                if (!capability.AktifMi)
                {
                    capability.AktifMi = true;
                    capability.UpdatedAt = now;
                    capability.UpdatedBy ??= "agent-heartbeat";
                }

                continue;
            }

            if (capability.AktifMi)
            {
                capability.AktifMi = false;
                capability.UpdatedAt = now;
                capability.UpdatedBy ??= "agent-heartbeat";
            }
        }

        foreach (var capability in normalizedIncoming)
        {
            if (existingSet.Contains(capability))
            {
                continue;
            }

            db.Set<AgentCapability>().Add(new AgentCapability
            {
                AgentId = agent.Id,
                KurumId = agent.KurumId,
                Capability = capability,
                AktifMi = true,
                CreatedBy = "agent-heartbeat",
                CreatedAt = now
            });
        }
    }

    private static string? NormalizeCapability(string? capability) =>
        string.IsNullOrWhiteSpace(capability) ? null : capability.Trim().ToLowerInvariant();
}
