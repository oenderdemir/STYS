using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using STYS.Agent.Contracts.Enums;
using STYS.Agent.Entities;
using STYS.Infrastructure.EntityFramework;

namespace STYS.Agent.Authorization;

public sealed class AgentCredentialRequirement : IAuthorizationRequirement { }

public sealed class AgentCredentialValidationHandler : AuthorizationHandler<AgentCredentialRequirement>
{
    private readonly IDbContextFactory<StysAppDbContext> _dbContextFactory;

    public AgentCredentialValidationHandler(IDbContextFactory<StysAppDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, AgentCredentialRequirement requirement)
    {
        var credentialIdClaim = context.User.FindFirst("credentialId")?.Value;
        var credentialVersionClaim = context.User.FindFirst("credentialVersion")?.Value;
        var agentIdClaim = context.User.FindFirst("agentId")?.Value;

        if (string.IsNullOrWhiteSpace(credentialIdClaim) || !int.TryParse(credentialIdClaim, out var credentialId)) return;
        if (string.IsNullOrWhiteSpace(credentialVersionClaim) || !int.TryParse(credentialVersionClaim, out var credentialVersion)) return;
        if (string.IsNullOrWhiteSpace(agentIdClaim) || !int.TryParse(agentIdClaim, out var agentId)) return;

        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var credential = await db.Set<AgentCredential>()
                .Include(x => x.Agent)
                .FirstOrDefaultAsync(x => x.Id == credentialId && !x.IsDeleted);

            if (credential is null) return;
            if (!credential.AktifMi || credential.RevokedAt.HasValue) return;
            if (credential.ExpiresAt.HasValue && DateTime.UtcNow > credential.ExpiresAt.Value) return;
            if (credential.CredentialVersion != credentialVersion) return;
            if (credential.AgentId != agentId) return;

            var agent = credential.Agent;
            if (agent is null || agent.IsDeleted) return;
            if (agent.KurumId != credential.KurumId) return;
            if (agent.Durum == AgentDurum.Disabled || agent.Durum == AgentDurum.Revoked || agent.Durum == AgentDurum.PendingApproval) return;

            context.Succeed(requirement);
        }
        catch { }
    }
}
