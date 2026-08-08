using Microsoft.Extensions.Logging;
using STYS.Agent.Client.Commands;

namespace STYS.Agent.Modules.Pavo.Commands;

public sealed class PavoConnectionTestCommandHandler : IAgentCommandHandler<PavoConnectionTestCommand>
{
    private readonly IPavoClient _pavoClient;
    private readonly ILogger<PavoConnectionTestCommandHandler> _logger;

    public PavoConnectionTestCommandHandler(IPavoClient pavoClient, ILogger<PavoConnectionTestCommandHandler> logger)
    {
        _pavoClient = pavoClient;
        _logger = logger;
    }

    public async Task<AgentCommandResult> HandleAsync(PavoConnectionTestCommand command, CancellationToken cancellationToken)
    {
        _logger.LogInformation("PAVO connection test başlatılıyor.");
        var result = await _pavoClient.TestConnectionAsync(
            Environment.GetEnvironmentVariable("PAVO_ENDPOINT") ?? "http://localhost:8080/health",
            5000,
            cancellationToken);

        if (result.Success)
        {
            _logger.LogInformation("PAVO bağlantısı başarılı ({ResponseTime}ms).", result.ResponseTimeMs);
            return AgentCommandResult.Ok($"PAVO connection OK ({result.ResponseTimeMs}ms)");
        }

        _logger.LogWarning("PAVO bağlantı testi başarısız: {Error}", result.ErrorMessage);
        return AgentCommandResult.Fail(result.ErrorMessage ?? "Bağlantı başarısız.");
    }
}
