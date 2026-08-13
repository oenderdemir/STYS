using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using STYS.Agent.Client.Infrastructure;
using STYS.Agent.Client.Upgrade;
using STYS.Agent.Contracts.Dtos;
using STYS.Agent.Contracts.Enums;
using STYS.Agent.Contracts.Versioning;
using STYS.Agent.Options;
using STYS.Agent.Updater.Options;
using ClientAgentApplyUpgradeRequest = STYS.Agent.Client.Upgrade.AgentApplyUpgradeRequest;

namespace STYS.Agent.Updater.Services;

public sealed class AgentUpgradeMonitorWorker : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IAgentPathResolver _paths;
    private readonly IAgentUpgradeRequestStore _requestStore;
    private readonly IAgentUpgradeOutcomeStore _outcomeStore;
    private readonly IAgentServiceController _serviceController;
    private readonly IAgentHealthProbe _healthProbe;
    private readonly AgentUpgradeOptions _trustOptions;
    private readonly AgentUpgradeRuntimeOptions _runtimeOptions;
    private readonly ILogger<AgentUpgradeMonitorWorker> _logger;

    public AgentUpgradeMonitorWorker(
        IAgentPathResolver paths,
        IAgentUpgradeRequestStore requestStore,
        IAgentUpgradeOutcomeStore outcomeStore,
        IAgentServiceController serviceController,
        IAgentHealthProbe healthProbe,
        IOptions<AgentUpgradeOptions> trustOptions,
        AgentUpgradeRuntimeOptions runtimeOptions,
        ILogger<AgentUpgradeMonitorWorker> logger)
    {
        _paths = paths;
        _requestStore = requestStore;
        _outcomeStore = outcomeStore;
        _serviceController = serviceController;
        _healthProbe = healthProbe;
        _trustOptions = trustOptions.Value;
        _runtimeOptions = runtimeOptions;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Upgrade monitor döngüsü başarısız.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(_runtimeOptions.PollIntervalSeconds, 1, 60)), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        var request = await _requestStore.GetAsync(cancellationToken);
        if (request is null || request.CommandId == Guid.Empty)
        {
            return;
        }

        var outcome = await _outcomeStore.GetAsync(cancellationToken);
        if (outcome is not null && outcome.CommandId == request.CommandId && outcome.ReportedAt.HasValue)
        {
            return;
        }

        var stageState = await ReadStageStateAsync(request, cancellationToken);
        if (stageState is null)
        {
            await WriteOutcomeAsync(request, AgentUpgradeOutcomeStatus.Failed, "Sahnelenmiş release bulunamadı.", cancellationToken);
            return;
        }

        if (!ValidateRequest(request, stageState))
        {
            await WriteOutcomeAsync(request, AgentUpgradeOutcomeStatus.Failed, "Apply isteği stage ile eşleşmiyor.", cancellationToken);
            return;
        }

        if (!VerifyPackage(stageState, request, out var packagePath, out var verificationError))
        {
            await WriteOutcomeAsync(request, AgentUpgradeOutcomeStatus.Failed, verificationError, cancellationToken);
            return;
        }

        var currentOutcome = outcome ?? new AgentUpgradeOutcome
        {
            CommandId = request.CommandId,
            ReleaseId = request.ReleaseId,
            Version = request.Version,
            RuntimeIdentifier = request.RuntimeIdentifier,
            Status = AgentUpgradeOutcomeStatus.Applying,
            Message = "Güncelleme uygulanıyor.",
            UpdatedAt = DateTimeOffset.UtcNow
        };

        if (currentOutcome.Status == AgentUpgradeOutcomeStatus.Applied || currentOutcome.Status == AgentUpgradeOutcomeStatus.RolledBack || currentOutcome.Status == AgentUpgradeOutcomeStatus.Failed)
        {
            return;
        }

        currentOutcome.Status = AgentUpgradeOutcomeStatus.Applying;
        currentOutcome.Message = "Güncelleme uygulanıyor.";
        currentOutcome.UpdatedAt = DateTimeOffset.UtcNow;
        await _outcomeStore.WriteAsync(currentOutcome, cancellationToken);

        var backupDirectory = Path.Combine(_paths.UpgradeBackupRootDirectory, request.CommandId.ToString("N"));
        Directory.CreateDirectory(backupDirectory);
        var applyRoot = Path.Combine(_paths.UpgradeTempRootDirectory, request.CommandId.ToString("N"));
        Directory.CreateDirectory(applyRoot);

        try
        {
            await _serviceController.StopAsync(cancellationToken);
            await _serviceController.WaitForStoppedAsync(TimeSpan.FromSeconds(30), cancellationToken);

            BackupInstallDirectory(backupDirectory);
            var extractDirectory = Path.Combine(applyRoot, "extract");
            AgentPackageExtractionGuard.ExtractPackage(packagePath!, extractDirectory);
            ReplaceInstallDirectory(extractDirectory);

            await _serviceController.StartAsync(cancellationToken);

            var healthy = await _healthProbe.WaitForHealthyAsync(_runtimeOptions.LocalUiPort, request.Version, TimeSpan.FromSeconds(Math.Clamp(_runtimeOptions.HealthTimeoutSeconds, 10, 300)), cancellationToken);
            if (!healthy)
            {
                await RollbackAsync(backupDirectory, "Sağlık kontrolü başarısız oldu.", cancellationToken);
                return;
            }

            await WriteOutcomeAsync(request, AgentUpgradeOutcomeStatus.Applied, "Güncelleme uygulandı.", cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Upgrade apply failed. CommandId={CommandId}", request.CommandId);
            await RollbackAsync(backupDirectory, SafeMessage(ex.Message), cancellationToken);
        }
    }

    private async Task RollbackAsync(string backupDirectory, string message, CancellationToken cancellationToken)
    {
        try
        {
            await _serviceController.StopAsync(cancellationToken);
            await _serviceController.WaitForStoppedAsync(TimeSpan.FromSeconds(30), cancellationToken);
        }
        catch
        {
        }

        try
        {
            if (Directory.Exists(backupDirectory))
            {
                RestoreInstallDirectory(backupDirectory);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rollback failed.");
            throw;
        }

        try
        {
            await _serviceController.StartAsync(cancellationToken);
            await _serviceController.WaitForRunningAsync(TimeSpan.FromSeconds(30), cancellationToken);
        }
        catch
        {
        }

        var request = await _requestStore.GetAsync(cancellationToken);
        if (request is not null)
        {
            await WriteOutcomeAsync(request, AgentUpgradeOutcomeStatus.RolledBack, message, cancellationToken);
        }
    }

    private async Task WriteOutcomeAsync(ClientAgentApplyUpgradeRequest request, AgentUpgradeOutcomeStatus status, string? message, CancellationToken cancellationToken)
    {
        var outcome = new AgentUpgradeOutcome
        {
            CommandId = request.CommandId,
            ReleaseId = request.ReleaseId,
            Version = request.Version,
            RuntimeIdentifier = request.RuntimeIdentifier,
            Status = status,
            Message = SafeMessage(message),
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await _outcomeStore.WriteAsync(outcome, cancellationToken);
    }

    private async Task<AgentReleaseStagingStateSnapshot?> ReadStageStateAsync(ClientAgentApplyUpgradeRequest request, CancellationToken cancellationToken)
    {
        var statePath = _paths.GetReleaseStagingStatePath(request.ReleaseId.ToString(System.Globalization.CultureInfo.InvariantCulture), request.RuntimeIdentifier);
        if (!File.Exists(statePath))
        {
            return null;
        }

        await using var stream = File.OpenRead(statePath);
        return await JsonSerializer.DeserializeAsync<AgentReleaseStagingStateSnapshot>(stream, JsonOptions, cancellationToken);
    }

    private static bool ValidateRequest(ClientAgentApplyUpgradeRequest request, AgentReleaseStagingStateSnapshot stageState)
    {
        return request.ReleaseId == stageState.ReleaseId
            && string.Equals(request.Version, stageState.Version, StringComparison.Ordinal)
            && string.Equals(request.RuntimeIdentifier, stageState.RuntimeIdentifier, StringComparison.OrdinalIgnoreCase)
            && string.Equals(request.Sha256, stageState.Sha256, StringComparison.OrdinalIgnoreCase)
            && string.Equals(request.Signature, stageState.Signature, StringComparison.Ordinal);
    }

    private bool VerifyPackage(AgentReleaseStagingStateSnapshot stageState, ClientAgentApplyUpgradeRequest request, out string? packagePath, out string? error)
    {
        packagePath = _paths.GetReleaseStagingPackagePath(request.ReleaseId.ToString(System.Globalization.CultureInfo.InvariantCulture), request.RuntimeIdentifier);
        error = null;

        if (!File.Exists(packagePath))
        {
            error = "Sahnelenmiş paket bulunamadı.";
            return false;
        }

        var packageBytes = File.ReadAllBytes(packagePath);
        if (stageState.PackageSize > 0 && packageBytes.LongLength != stageState.PackageSize)
        {
            error = "Paket boyutu eşleşmiyor.";
            return false;
        }

        var actualHash = Convert.ToHexString(SHA256.HashData(packageBytes));
        if (!string.Equals(actualHash, request.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            error = "Paket hash doğrulaması başarısız.";
            return false;
        }

        if (!VerifySignature(stageState, request))
        {
            error = "Paket imza doğrulaması başarısız.";
            return false;
        }

        return true;
    }

    private bool VerifySignature(AgentReleaseStagingStateSnapshot stageState, ClientAgentApplyUpgradeRequest request)
    {
        if (string.IsNullOrWhiteSpace(_trustOptions.ReleasePublicKeyPem))
        {
            return false;
        }

        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(_trustOptions.ReleasePublicKeyPem);
            var payload = AgentReleaseManifest.BuildSignaturePayload(new AgentStageUpgradeRequest
            {
                ReleaseId = request.ReleaseId,
                Version = request.Version,
                ContractVersion = stageState.ContractVersion,
                RuntimeIdentifier = request.RuntimeIdentifier,
                Sha256 = request.Sha256,
                PackageSize = stageState.PackageSize,
                PublishedAt = stageState.PublishedAt,
                ReleaseNotes = null,
                Signature = request.Signature
            });
            var signature = Convert.FromBase64String(request.Signature);
            return rsa.VerifyData(payload, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        }
        catch
        {
            return false;
        }
    }

    private void BackupInstallDirectory(string backupDirectory)
    {
        if (Directory.Exists(backupDirectory))
        {
            Directory.Delete(backupDirectory, true);
        }

        Directory.CreateDirectory(backupDirectory);
        CopyDirectory(_runtimeOptions.InstallDirectory, backupDirectory, preserveExisting: false);
    }

    private void ReplaceInstallDirectory(string extractedDirectory)
    {
        if (!Directory.Exists(_runtimeOptions.InstallDirectory))
        {
            Directory.CreateDirectory(_runtimeOptions.InstallDirectory);
        }

        foreach (var file in Directory.EnumerateFiles(_runtimeOptions.InstallDirectory, "*", SearchOption.AllDirectories))
        {
            if (ShouldPreserveFile(file))
            {
                continue;
            }

            File.Delete(file);
        }

        foreach (var directory in Directory.EnumerateDirectories(_runtimeOptions.InstallDirectory, "*", SearchOption.AllDirectories).OrderByDescending(x => x.Length))
        {
            if (!Directory.Exists(directory))
            {
                continue;
            }

            if (Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Length == 0
                && Directory.GetDirectories(directory, "*", SearchOption.AllDirectories).Length == 0)
            {
                Directory.Delete(directory, false);
            }
        }

        CopyDirectory(extractedDirectory, _runtimeOptions.InstallDirectory, preserveExisting: true);
    }

    private void RestoreInstallDirectory(string backupDirectory)
    {
        foreach (var file in Directory.EnumerateFiles(_runtimeOptions.InstallDirectory, "*", SearchOption.AllDirectories))
        {
            if (ShouldPreserveFile(file))
            {
                continue;
            }

            File.Delete(file);
        }

        foreach (var directory in Directory.EnumerateDirectories(_runtimeOptions.InstallDirectory, "*", SearchOption.AllDirectories).OrderByDescending(x => x.Length))
        {
            if (Directory.Exists(directory) && Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Length == 0 && Directory.GetDirectories(directory, "*", SearchOption.AllDirectories).Length == 0)
            {
                Directory.Delete(directory, false);
            }
        }

        CopyDirectory(backupDirectory, _runtimeOptions.InstallDirectory, preserveExisting: false);
    }

    private static void CopyDirectory(string sourceDirectory, string targetDirectory, bool preserveExisting)
    {
        if (!Directory.Exists(sourceDirectory))
        {
            return;
        }

        Directory.CreateDirectory(targetDirectory);

        foreach (var directory in Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(targetDirectory, relative));
        }

        foreach (var file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDirectory, file);
            var targetFile = Path.Combine(targetDirectory, relative);
            var targetParent = Path.GetDirectoryName(targetFile);
            if (!string.IsNullOrWhiteSpace(targetParent))
            {
                Directory.CreateDirectory(targetParent);
            }

            if (preserveExisting && ShouldPreservePath(targetFile) && File.Exists(targetFile))
            {
                continue;
            }

            File.Copy(file, targetFile, overwrite: true);
        }
    }

    private static bool ShouldPreservePath(string path)
    {
        var fileName = Path.GetFileName(path);
        return fileName.StartsWith("appsettings", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("bootstrap.json", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldPreserveFile(string path) => ShouldPreservePath(path);

    private static string SafeMessage(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Upgrade işlemi başarısız.";
        }

        return value.Trim();
    }

    private sealed class AgentReleaseStagingStateSnapshot
    {
        public int ReleaseId { get; set; }
        public string Version { get; set; } = string.Empty;
        public string ContractVersion { get; set; } = string.Empty;
        public string RuntimeIdentifier { get; set; } = string.Empty;
        public AgentReleaseStageStatus StageStatus { get; set; }
        public string? Message { get; set; }
        public string Sha256 { get; set; } = string.Empty;
        public string Signature { get; set; } = string.Empty;
        public long PackageSize { get; set; }
        public DateTimeOffset PublishedAt { get; set; }
        public string? PackagePath { get; set; }
    }
}
