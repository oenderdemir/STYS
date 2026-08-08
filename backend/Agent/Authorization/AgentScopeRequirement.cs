using Microsoft.AspNetCore.Authorization;

namespace STYS.Agent.Authorization;

public sealed class AgentScopeRequirement : IAuthorizationRequirement
{
    public string RequiredScope { get; }

    public AgentScopeRequirement(string requiredScope)
    {
        RequiredScope = requiredScope;
    }
}

public sealed class AgentScopeAuthorizationHandler : AuthorizationHandler<AgentScopeRequirement>
{
    private readonly ICurrentAgentContext _agentContext;

    public AgentScopeAuthorizationHandler(ICurrentAgentContext agentContext)
    {
        _agentContext = agentContext;
    }

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AgentScopeRequirement requirement)
    {
        if (!_agentContext.IsAuthenticated)
            return Task.CompletedTask;

        if (_agentContext.Scopes.Contains(requirement.RequiredScope, StringComparer.OrdinalIgnoreCase))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
