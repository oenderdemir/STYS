using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using STYS.Agent.Client;
using STYS.Agent.Client.Authentication;
using STYS.Agent.Client.Commands;
using STYS.Agent.Client.Upgrade;
using STYS.Agent.Contracts.Dtos;
using STYS.Agent.Upgrade;
using STYS.Agent.Services;

namespace STYS.Agent.Workers;

public sealed class AgentUpgradeOutcomeReporterWorker : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IStysAgentApiClient _client;
    private readonly IAgentAuthenticationState _authenticationState;
    private readonly IAgentUpgradeRequestStore _requestStore;
    private readonly IAgentUpgradeOutcomeStore _outcomeStore;
    private readonly ILogger<AgentUpgradeOutcomeReporterWorker> _logger;

    public AgentUpgradeOutcomeReporterWorker(
        IStysAgentApiClient client,
        IAgentAuthenticationState authenticationState,
        IAgentUpgradeRequestStore requestStore,
        IAgentUpgradeOutcomeStore outcomeStore,
        ILogger<AgentUpgradeOutcomeReporterWorker> logger)
    {
        _client = client;
        _authenticationState = authenticationState;
        _requestStore = requestStore;
        _outcomeStore = outcomeStore;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _authenticationState.WaitUntilReadyAsync(stoppingToken);
                await TryReportOutcomeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Upgrade outcome report döngüsü başarısız.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task TryReportOutcomeAsync(CancellationToken cancellationToken)
    {
        var request = await _requestStore.GetAsync(cancellationToken);
        if (request is null || request.CommandId == Guid.Empty)
        {
            return;
        }

        var outcome = await _outcomeStore.GetAsync(cancellationToken);
        if (outcome is null || outcome.CommandId != request.CommandId || outcome.ReportedAt.HasValue)
        {
            return;
        }

        var response = new AgentApplyUpgradeResponse
        {
            CommandId = request.CommandId,
            ReleaseId = request.ReleaseId,
            Version = request.Version,
            RuntimeIdentifier = request.RuntimeIdentifier,
            ApplyStatus = outcome.Status.ToString(),
            Message = outcome.Message
        };

        var completeRequest = new AgentCommandCompleteRequest
        {
            Id = request.CommandId,
            Success = outcome.Status == AgentUpgradeOutcomeStatus.Applied,
            ResultPayload = JsonSerializer.Serialize(response, JsonOptions),
            ErrorCode = outcome.Status == AgentUpgradeOutcomeStatus.Applied ? null : $"UPGRADE_{outcome.Status.ToString().ToUpperInvariant()}",
            ErrorMessage = outcome.Message
        };

        try
        {
            await _client.CompleteCommandAsync(request.CommandId, completeRequest, cancellationToken);
            await _outcomeStore.MarkReportedAsync(request.CommandId, cancellationToken);
            _logger.LogInformation("Upgrade outcome STYS'e raporlandı. CommandId={CommandId}, Status={Status}", request.CommandId, outcome.Status);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Upgrade outcome raporlanamadı. CommandId={CommandId}", request.CommandId);
        }
    }
}
