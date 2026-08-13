using Microsoft.Extensions.DependencyInjection;
using STYS.Agent.Client.Commands;
using STYS.Agent.Client.Upgrade;

namespace STYS.Agent.Services;

public static class AgentProductionServiceCollectionExtensions
{
    public static IServiceCollection AddAgentProductionInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IAgentStartupValidationService, AgentStartupValidationService>();
        services.AddSingleton<IAgentCommandExecutionStore, FileAgentCommandExecutionStore>();
        services.AddSingleton<IAgentUpgradeRequestStore, FileAgentUpgradeRequestStore>();
        services.AddSingleton<IAgentUpgradeOutcomeStore, FileAgentUpgradeOutcomeStore>();
        return services;
    }
}
