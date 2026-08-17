using System.Text.Json;
using Microsoft.Extensions.Logging;
using STYS.Agent.Client.Commands;
using STYS.Agent.Client.Infrastructure;
using STYS.Agent.Client.Upgrade;
using STYS.Agent.Contracts.Dtos;
using STYS.Agent.Contracts.Enums;
using STYS.Agent.Options;
using ClientApplyUpgradeRequest = STYS.Agent.Client.Upgrade.AgentApplyUpgradeRequest;

namespace STYS.Agent.Upgrade;

public sealed class AgentApplyUpgradeCommandHandler : IAgentCommandHandler<AgentApplyUpgradeCommand>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IAgentUpgradeRequestStore _requestStore;
    private readonly IAgentReleaseStagingStore _stagingStore;
    private readonly IAgentPathResolver _paths;
    private readonly AgentUpgradeOptions _options;
    private readonly ILogger<AgentApplyUpgradeCommandHandler> _logger;

    public AgentApplyUpgradeCommandHandler(
        IAgentUpgradeRequestStore requestStore,
        IAgentReleaseStagingStore stagingStore,
        IAgentPathResolver paths,
        Microsoft.Extensions.Options.IOptions<AgentUpgradeOptions> options,
        ILogger<AgentApplyUpgradeCommandHandler> logger)
    {
        _requestStore = requestStore;
        _stagingStore = stagingStore;
        _paths = paths;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AgentCommandResult> HandleAsync(AgentApplyUpgradeCommand command, CancellationToken cancellationToken)
    {
        if (command.CommandId == Guid.Empty)
        {
            throw new InvalidOperationException("Apply komutu kimliği zorunludur.");
        }

        var stageState = await _stagingStore.GetAsync(command.ReleaseId, cancellationToken)
            ?? throw new InvalidOperationException("Sahnelenmiş release bulunamadı.");

        if (stageState.StageStatus != AgentReleaseStageStatus.Staged)
        {
            throw new InvalidOperationException("Sahnelenmiş release apply için hazır değil.");
        }

        if (!string.Equals(stageState.Version, command.Version, StringComparison.Ordinal)
            || !string.Equals(stageState.RuntimeIdentifier, command.RuntimeIdentifier, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(stageState.Sha256, command.Sha256, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(stageState.Signature, command.Signature, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Apply komutu staged release ile eşleşmiyor.");
        }

        var stagedPackagePath = _paths.GetReleaseStagingPackagePath(command.ReleaseId.ToString(System.Globalization.CultureInfo.InvariantCulture), command.RuntimeIdentifier);
        if (!File.Exists(stagedPackagePath))
        {
            throw new InvalidOperationException("Sahnelenmiş paket bulunamadı.");
        }

        EnsureUpdaterAvailable();

        var request = new ClientApplyUpgradeRequest
        {
            CommandId = command.CommandId,
            LeaseToken = command.LeaseToken,
            ReleaseId = command.ReleaseId,
            Version = command.Version,
            RuntimeIdentifier = command.RuntimeIdentifier,
            Sha256 = command.Sha256,
            Signature = command.Signature
        };

        var existing = await _requestStore.GetAsync(cancellationToken);
        if (existing is not null)
        {
            if (existing.CommandId == Guid.Empty)
            {
                await _requestStore.WriteAsync(request, cancellationToken);
            }
            else if (existing.CommandId != command.CommandId
                || existing.ReleaseId != request.ReleaseId
                || !string.Equals(existing.Version, request.Version, StringComparison.Ordinal)
                || !string.Equals(existing.RuntimeIdentifier, request.RuntimeIdentifier, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(existing.Sha256, request.Sha256, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(existing.Signature, request.Signature, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Mevcut apply request staged release ile eşleşmiyor.");
            }
        }
        else
        {
            await _requestStore.WriteAsync(request, cancellationToken);
            _logger.LogInformation("Apply request yazıldı. ReleaseId={ReleaseId}, Version={Version}, RID={RuntimeIdentifier}", command.ReleaseId, command.Version, command.RuntimeIdentifier);
        }

        var response = new AgentApplyUpgradeResponse
        {
            CommandId = command.CommandId,
            ReleaseId = command.ReleaseId,
            Version = command.Version,
            RuntimeIdentifier = command.RuntimeIdentifier,
            ApplyStatus = "Applying",
            Message = "Güncelleme isteği yazıldı."
        };

        return AgentCommandResult.Ok(JsonSerializer.Serialize(response, JsonOptions), deferCompletion: true);
    }

    public const string UpdaterNotAvailableCode = "AGENT_UPDATER_NOT_AVAILABLE";

    /// <summary>
    /// The apply command completes asynchronously: this handler only records the request and the
    /// updater service carries it out. If that service is not installed nothing would ever pick the
    /// request up and the command would hang until it expired, so refuse up front instead.
    /// Detection failures are treated as "present" so a working install is never blocked by a
    /// permissions quirk in the service query.
    /// </summary>
    private void EnsureUpdaterAvailable()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var serviceName = _options.UpdaterServiceName?.Trim();
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            return;
        }

        bool installed;
        try
        {
            installed = System.ServiceProcess.ServiceController
                .GetServices()
                .Any(x => string.Equals(x.ServiceName, serviceName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(x.DisplayName, serviceName, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Updater servis durumu sorgulanamadı; apply isteği yine de yazılacak.");
            return;
        }

        if (!installed)
        {
            _logger.LogError("Updater servisi bulunamadı: {ServiceName}", serviceName);
            throw new InvalidOperationException(
                $"{UpdaterNotAvailableCode}: '{serviceName}' servisi kurulu değil, güncelleme uygulanamaz.");
        }
    }
}
