using Microsoft.Extensions.Logging;
using STYS.Agent.Client.Commands;

namespace STYS.Agent.Modules.Pavo.Commands;

public sealed class PavoConnectionTestCommandHandler : IAgentCommandHandler<PavoConnectionTestCommand>
{
    private readonly ILogger<PavoConnectionTestCommandHandler> _logger;

    public PavoConnectionTestCommandHandler(ILogger<PavoConnectionTestCommandHandler> logger)
    {
        _logger = logger;
    }

    public async Task<AgentCommandResult> HandleAsync(PavoConnectionTestCommand command, CancellationToken cancellationToken)
    {
        _logger.LogInformation("PAVO connection test started.");
        await Task.Delay(100, cancellationToken);
        _logger.LogInformation("PAVO connection test completed successfully (stub).");
        return AgentCommandResult.Ok("PAVO stub — connection successful.");
    }
}
