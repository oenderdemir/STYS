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
    private readonly IUpdaterServicePresenceProbe _updaterProbe;
    private readonly ILogger<AgentApplyUpgradeCommandHandler> _logger;

    public AgentApplyUpgradeCommandHandler(
        IAgentUpgradeRequestStore requestStore,
        IAgentReleaseStagingStore stagingStore,
        IAgentPathResolver paths,
        Microsoft.Extensions.Options.IOptions<AgentUpgradeOptions> options,
        IUpdaterServicePresenceProbe updaterProbe,
        ILogger<AgentApplyUpgradeCommandHandler> logger)
    {
        _requestStore = requestStore;
        _stagingStore = stagingStore;
        _paths = paths;
        _options = options.Value;
        _updaterProbe = updaterProbe;
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

        // Checked before anything is written: the updater is what actually performs the upgrade, so
        // without it the request file would sit unread and this command would defer forever.
        var updaterAvailability = CheckUpdaterAvailability();
        if (updaterAvailability is not null)
        {
            return updaterAvailability;
        }

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
    public const string UpdaterStatusUnknownCode = "AGENT_UPDATER_STATUS_UNKNOWN";
    public const string UpdaterConfigurationInvalidCode = "AGENT_UPDATER_CONFIGURATION_INVALID";
    public const string UpdaterNotRunningCode = "AGENT_UPDATER_NOT_RUNNING";

    /// <summary>
    /// Verifies that the updater service exists. Returns a failed result to report on, or null when
    /// the apply may proceed.
    ///
    /// Returning a result rather than throwing matters: an exception is reported to the backend as
    /// a generic HANDLER_EXCEPTION, which hides the real reason from command history and the UI.
    ///
    /// Fail closed when the service cannot be queried. Deferring on an unverified assumption means
    /// that if the updater really is absent the command waits until it expires, with no signal of
    /// why; a clear refusal is recoverable, a silent hang is not.
    /// </summary>
    private AgentCommandResult? CheckUpdaterAvailability()
    {
        var serviceName = _options.UpdaterServiceName?.Trim();
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            // A blank service name is a deployment misconfiguration, not permission to skip the
            // check: proceeding would write a request no service is known to consume.
            _logger.LogError("Updater servis adı yapılandırılmamış; apply reddedildi.");
            return AgentCommandResult.Fail(
                "Updater servis adı yapılandırılmamış, güncelleme uygulanamaz.",
                UpdaterConfigurationInvalidCode);
        }

        return _updaterProbe.Check(serviceName) switch
        {
            UpdaterPresence.Present => null,
            UpdaterPresence.Missing => AgentCommandResult.Fail(
                $"'{serviceName}' servisi kurulu değil, güncelleme uygulanamaz.",
                UpdaterNotAvailableCode),
            UpdaterPresence.NotRunning => AgentCommandResult.Fail(
                $"'{serviceName}' servisi çalışmıyor, güncelleme uygulanamaz.",
                UpdaterNotRunningCode),
            _ => AgentCommandResult.Fail(
                $"Updater servis durumu doğrulanamadı ('{serviceName}'), güncelleme uygulanmadı.",
                UpdaterStatusUnknownCode)
        };
    }
}
