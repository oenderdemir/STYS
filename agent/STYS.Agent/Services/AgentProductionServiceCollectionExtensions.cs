using Microsoft.Extensions.DependencyInjection;
using STYS.Agent.Client.Commands;

namespace STYS.Agent.Services;

public static class AgentProductionServiceCollectionExtensions
{
    public static IServiceCollection AddAgentProductionInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IAgentStartupValidationService, AgentStartupValidationService>();
        services.AddSingleton<IAgentCommandExecutionStore, FileAgentCommandExecutionStore>();
        return services;
    }
}
