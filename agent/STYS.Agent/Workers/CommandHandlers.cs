using Microsoft.Extensions.Logging;
using STYS.Agent.Client.Commands;

namespace STYS.Agent.Workers;

internal sealed class PingCommand : IAgentCommand { public string CommandType => "Ping"; }
internal sealed class HealthCheckCommand : IAgentCommand { public string CommandType => "HealthCheck"; }
internal sealed class RefreshConfigurationCommand : IAgentCommand { public string CommandType => "RefreshConfiguration"; }

internal sealed class PingCommandHandler(ILogger<PingCommandHandler> logger) : IAgentCommandHandler<PingCommand>
{
    public Task<AgentCommandResult> HandleAsync(PingCommand command, CancellationToken cancellationToken)
    {
        logger.LogInformation("Ping received.");
        return Task.FromResult(AgentCommandResult.Ok("pong"));
    }
}

internal sealed class HealthCheckCommandHandler(ILogger<HealthCheckCommandHandler> logger) : IAgentCommandHandler<HealthCheckCommand>
{
    public Task<AgentCommandResult> HandleAsync(HealthCheckCommand command, CancellationToken cancellationToken)
    {
        logger.LogInformation("Health check OK.");
        return Task.FromResult(AgentCommandResult.Ok("healthy"));
    }
}

internal sealed class RefreshConfigCommandHandler(ILogger<RefreshConfigCommandHandler> logger) : IAgentCommandHandler<RefreshConfigurationCommand>
{
    public Task<AgentCommandResult> HandleAsync(RefreshConfigurationCommand command, CancellationToken cancellationToken)
    {
        logger.LogInformation("Configuration refresh triggered.");
        return Task.FromResult(AgentCommandResult.Ok("config-refreshed"));
    }
}
