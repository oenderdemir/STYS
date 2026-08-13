using System.Text.Json;
using Microsoft.Extensions.Logging;
using STYS.Agent.Client.Commands;
using STYS.Agent.Contracts.Dtos;

namespace STYS.Agent.Upgrade;

public sealed class AgentStageUpgradeCommandHandler : IAgentCommandHandler<AgentStageUpgradeCommand>
{
    private readonly IAgentReleaseStagingService _stagingService;
    private readonly ILogger<AgentStageUpgradeCommandHandler> _logger;

    public AgentStageUpgradeCommandHandler(IAgentReleaseStagingService stagingService, ILogger<AgentStageUpgradeCommandHandler> logger)
    {
        _stagingService = stagingService;
        _logger = logger;
    }

    public async Task<AgentCommandResult> HandleAsync(AgentStageUpgradeCommand command, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Agent upgrade staging başlatılıyor: {Version} / {RuntimeIdentifier}", command.Version, command.RuntimeIdentifier);
        var result = await _stagingService.StageAsync(command, cancellationToken);

        if (string.IsNullOrWhiteSpace(result.ResultPayload))
        {
            var response = new AgentStageUpgradeResponse
            {
                Version = command.Version,
                RuntimeIdentifier = command.RuntimeIdentifier,
                StageStatus = result.Success ? STYS.Agent.Contracts.Enums.AgentReleaseStageStatus.Staged : STYS.Agent.Contracts.Enums.AgentReleaseStageStatus.Failed,
                Message = result.ErrorMessage
            };
            result.ResultPayload = JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }

        return result;
    }
}
