using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using TOD.Platform.AspNetCore.Authorization;

namespace STYS.Agent.Authorization;

public static class AgentAuthorizationExtensions
{
    public static IServiceCollection AddAgentAuthorization(this IServiceCollection services)
    {
        services.AddScoped<IAuthorizationHandler, AgentCredentialValidationHandler>();
        services.AddScoped<IAuthorizationHandler, AgentScopeAuthorizationHandler>();

        services.AddAuthorization(authOptions =>
        {
            authOptions.AddPolicy(TodPlatformAuthorizationConstants.AgentPolicy, policy =>
            {
                policy.RequireAuthenticatedUser()
                    .RequireClaim("agentId")
                    .AddAuthenticationSchemes(TodPlatformAuthorizationConstants.AgentScheme);
                policy.AddRequirements(new AgentCredentialRequirement());
            });

            authOptions.AddPolicy(AgentPolicies.AgentHeartbeat, policy =>
            {
                policy.RequireAuthenticatedUser()
                    .AddAuthenticationSchemes(TodPlatformAuthorizationConstants.AgentScheme);
                policy.AddRequirements(new AgentCredentialRequirement(), new AgentScopeRequirement(AgentPolicies.AgentHeartbeat));
            });

            authOptions.AddPolicy(AgentPolicies.AgentConfigRead, policy =>
            {
                policy.RequireAuthenticatedUser()
                    .AddAuthenticationSchemes(TodPlatformAuthorizationConstants.AgentScheme);
                policy.AddRequirements(new AgentCredentialRequirement(), new AgentScopeRequirement(AgentPolicies.AgentConfigRead));
            });

            authOptions.AddPolicy(AgentPolicies.AgentCommandRead, policy =>
            {
                policy.RequireAuthenticatedUser()
                    .AddAuthenticationSchemes(TodPlatformAuthorizationConstants.AgentScheme);
                policy.AddRequirements(new AgentCredentialRequirement(), new AgentScopeRequirement(AgentPolicies.AgentCommandRead));
            });

            authOptions.AddPolicy(AgentPolicies.AgentCommandExecute, policy =>
            {
                policy.RequireAuthenticatedUser()
                    .AddAuthenticationSchemes(TodPlatformAuthorizationConstants.AgentScheme);
                policy.AddRequirements(new AgentCredentialRequirement(), new AgentScopeRequirement(AgentPolicies.AgentCommandExecute));
            });

            authOptions.AddPolicy(AgentPolicies.AgentResultWrite, policy =>
            {
                policy.RequireAuthenticatedUser()
                    .AddAuthenticationSchemes(TodPlatformAuthorizationConstants.AgentScheme);
                policy.AddRequirements(new AgentCredentialRequirement(), new AgentScopeRequirement(AgentPolicies.AgentResultWrite));
            });
        });

        return services;
    }
}
