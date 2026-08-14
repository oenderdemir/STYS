using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using STYS.Agent.Client;
using STYS.Agent.Client.Infrastructure;
using STYS.Agent.Client.Commands;
using STYS.Agent.Contracts.Dtos;
using STYS.Agent.Contracts.Enums;
using STYS.Agent.Options;

namespace STYS.Agent.Upgrade;

public sealed class AgentReleaseStagingService : IAgentReleaseStagingService
{
    private readonly IStysAgentApiClient _client;
    private readonly IAgentPathResolver _paths;
    private readonly IAgentReleaseStagingStore _store;
    private readonly AgentUpgradeOptions _options;
    private readonly ILogger<AgentReleaseStagingService> _logger;

    public AgentReleaseStagingService(
        IStysAgentApiClient client,
        IAgentPathResolver paths,
        IAgentReleaseStagingStore store,
        IOptions<AgentUpgradeOptions> options,
        ILogger<AgentReleaseStagingService> logger)
    {
        _client = client;
        _paths = paths;
        _store = store;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AgentCommandResult> StageAsync(AgentStageUpgradeCommand command, CancellationToken cancellationToken)
    {
        if (command.ReleaseId <= 0 || string.IsNullOrWhiteSpace(command.Version) || string.IsNullOrWhiteSpace(command.RuntimeIdentifier))
        {
            return AgentCommandResult.Fail("Güncelleme manifesti eksik.", "INVALID_RELEASE_MANIFEST");
        }

        var stageDirectory = _paths.GetReleaseStagingDirectory(command.ReleaseId.ToString(System.Globalization.CultureInfo.InvariantCulture), command.RuntimeIdentifier);
        Directory.CreateDirectory(stageDirectory);

        var existing = await _store.GetAsync(command.ReleaseId, cancellationToken);
        if (existing is not null && existing.StageStatus == AgentReleaseStageStatus.Staged)
        {
            var stagedPackagePath = existing.PackagePath ?? _paths.GetReleaseStagingPackagePath(command.ReleaseId.ToString(System.Globalization.CultureInfo.InvariantCulture), command.RuntimeIdentifier);
            if (File.Exists(stagedPackagePath))
            {
                var stagedBytes = await File.ReadAllBytesAsync(stagedPackagePath, cancellationToken);
                var stagedHash = Convert.ToHexString(SHA256.HashData(stagedBytes));
                if (!string.Equals(stagedHash, command.Sha256, StringComparison.OrdinalIgnoreCase)
                    || !AgentReleaseSignatureVerifier.Verify(command.ToRequest(), _options.ReleasePublicKeyPem))
                {
                    throw new InvalidOperationException("Mevcut sahnelenmiş paket doğrulanamadı.");
                }

                var alreadyStaged = CreateResponse(command, AgentReleaseStageStatus.Staged, "Paket zaten sahnelenmiş.", stagedPackagePath);
                return AgentCommandResult.Ok(System.Text.Json.JsonSerializer.Serialize(alreadyStaged));
            }
        }

        var state = new AgentReleaseStagingState
        {
            ReleaseId = command.ReleaseId,
            Version = command.Version,
            ContractVersion = command.ContractVersion,
            RuntimeIdentifier = command.RuntimeIdentifier,
            Sha256 = command.Sha256,
            Signature = command.Signature,
            PackageSize = command.PackageSize,
            PublishedAt = command.PublishedAt,
            StageStatus = AgentReleaseStageStatus.Downloading,
            Message = "Paket indiriliyor.",
            DownloadingAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await _store.UpsertAsync(state, cancellationToken);

        var tempPath = Path.Combine(stageDirectory, $"{Path.GetRandomFileName()}.tmp");
        var finalPath = _paths.GetReleaseStagingPackagePath(command.ReleaseId.ToString(System.Globalization.CultureInfo.InvariantCulture), command.RuntimeIdentifier);

        try
        {
            var packageBytes = await _client.DownloadReleasePackageAsync(command.ReleaseId, cancellationToken);
            await File.WriteAllBytesAsync(tempPath, packageBytes, cancellationToken);

            if (command.PackageSize > 0 && packageBytes.LongLength != command.PackageSize)
            {
                throw new InvalidOperationException("İndirilen paket boyutu beklenen imzalı manifest ile eşleşmiyor.");
            }

            state.StageStatus = AgentReleaseStageStatus.Verifying;
            state.Message = "Paket doğrulanıyor.";
            state.VerifyingAt = DateTimeOffset.UtcNow;
            state.UpdatedAt = DateTimeOffset.UtcNow;
            await _store.UpsertAsync(state, cancellationToken);

            var actualSha = Convert.ToHexString(SHA256.HashData(packageBytes));
            if (!string.Equals(actualSha, command.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Paket hash doğrulaması başarısız.");
            }

            if (!AgentReleaseSignatureVerifier.Verify(command.ToRequest(), _options.ReleasePublicKeyPem))
            {
                throw new InvalidOperationException("Paket imzası doğrulanamadı.");
            }

            ReplaceAtomically(tempPath, finalPath);

            state.StageStatus = AgentReleaseStageStatus.Staged;
            state.Message = "Paket sahnelendi.";
            state.PackagePath = finalPath;
            state.StagedAt = DateTimeOffset.UtcNow;
            state.UpdatedAt = DateTimeOffset.UtcNow;
            await _store.UpsertAsync(state, cancellationToken);

            return AgentCommandResult.Ok(System.Text.Json.JsonSerializer.Serialize(CreateResponse(command, AgentReleaseStageStatus.Staged, "Paket sahnelendi.", finalPath)));
        }
        catch (Exception ex)
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
            }

            state.StageStatus = AgentReleaseStageStatus.Failed;
            state.Message = SafeMessage(ex.Message);
            state.FailedAt = DateTimeOffset.UtcNow;
            state.UpdatedAt = DateTimeOffset.UtcNow;
            await _store.UpsertAsync(state, cancellationToken);

            _logger.LogWarning(ex, "Agent release staging failed for {ReleaseId} {Version}/{RuntimeIdentifier}", command.ReleaseId, command.Version, command.RuntimeIdentifier);
            var failure = AgentCommandResult.Fail(state.Message, "RELEASE_STAGING_FAILED");
            failure.ResultPayload = System.Text.Json.JsonSerializer.Serialize(CreateResponse(command, AgentReleaseStageStatus.Failed, state.Message, null));
            return failure;
        }
    }

    private static AgentStageUpgradeResponse CreateResponse(AgentStageUpgradeCommand command, AgentReleaseStageStatus status, string? message, string? packagePath) =>
        new()
        {
            ReleaseId = command.ReleaseId,
            Version = command.Version,
            RuntimeIdentifier = command.RuntimeIdentifier,
            StageStatus = status,
            Message = packagePath is null ? message : $"{message} ({Path.GetFileName(packagePath)})"
        };

    private static string SafeMessage(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Güncelleme sahnelenemedi.";
        }

        return value.Trim();
    }

    private static void ReplaceAtomically(string tempPath, string targetPath)
    {
        try
        {
            File.Move(tempPath, targetPath, overwrite: true);
        }
        catch (IOException)
        {
            if (File.Exists(targetPath))
            {
                try { File.Delete(targetPath); } catch { }
            }

            File.Move(tempPath, targetPath, overwrite: true);
        }
    }
}
