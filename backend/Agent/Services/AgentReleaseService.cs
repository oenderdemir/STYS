using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using STYS.Agent.Authorization;
using STYS.Agent.Contracts.Dtos;
using STYS.Agent.Contracts.Enums;
using STYS.Agent.Contracts.Versioning;
using STYS.Agent.Entities;
using STYS.Agent.Options;
using STYS.Infrastructure.EntityFramework;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;
using AgentEntity = STYS.Agent.Entities.Agent;

namespace STYS.Agent.Services;

public sealed class AgentReleaseService : IAgentReleaseService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IDbContextFactory<StysAppDbContext> _dbContextFactory;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly ICurrentAgentContext _currentAgentContext;
    private readonly AgentCommandService _commandService;
    private readonly AgentCompatibilityOptions _compatibilityOptions;

    public AgentReleaseService(
        IDbContextFactory<StysAppDbContext> dbContextFactory,
        ICurrentTenantAccessor tenantAccessor,
        ICurrentAgentContext currentAgentContext,
        AgentCommandService commandService,
        IOptions<AgentCompatibilityOptions>? compatibilityOptions = null)
    {
        _dbContextFactory = dbContextFactory;
        _tenantAccessor = tenantAccessor;
        _currentAgentContext = currentAgentContext;
        _commandService = commandService;
        _compatibilityOptions = compatibilityOptions?.Value ?? new AgentCompatibilityOptions();
    }

    public async Task<AgentCommandDto> StageUpgradeAsync(int agentId, string requestedBy, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var agent = await db.Set<AgentEntity>()
            .FirstOrDefaultAsync(x => x.Id == agentId && !x.IsDeleted, cancellationToken)
            ?? throw new BaseException("Agent bulunamadı.", 404);

        EnforceTenantAccess(agent.KurumId);

        var release = await SelectBestReleaseAsync(db, agent, cancellationToken);
        if (release is null)
        {
            throw new BaseException("Uygun imzalı release bulunamadı.", 404);
        }

        var payload = new AgentStageUpgradeRequest
        {
            ReleaseId = release.Id,
            Version = release.Version,
            ContractVersion = release.ContractVersion,
            RuntimeIdentifier = release.RuntimeIdentifier,
            Sha256 = release.Sha256,
            Signature = release.Signature,
            PackageSize = release.PackageSize,
            PublishedAt = release.PublishedAt,
            ReleaseNotes = release.ReleaseNotes
        };

        return await _commandService.SendAsync(new AgentCommandSendRequest
        {
            AgentId = agent.Id,
            CommandType = "AgentStageUpgrade",
            Payload = JsonSerializer.Serialize(payload, JsonOptions),
            Priority = 1,
            ExpirationMinutes = 60,
            MaxRetryCount = 1
        }, requestedBy, cancellationToken);
    }

    public async Task<AgentCommandDto> ApplyUpgradeAsync(int agentId, string requestedBy, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var agent = await db.Set<AgentEntity>()
            .FirstOrDefaultAsync(x => x.Id == agentId && !x.IsDeleted, cancellationToken)
            ?? throw new BaseException("Agent bulunamadı.", 404);

        EnforceTenantAccess(agent.KurumId);

        var staged = await GetStagedReleaseAsync(db, agent, cancellationToken);
        if (staged is null)
        {
            throw new BaseException("Uygun sahnelenmiş release bulunamadı.", 404);
        }

        var applyRequest = new AgentApplyUpgradeRequest
        {
            CommandId = Guid.Empty,
            ReleaseId = staged.Release.Id,
            Version = staged.Release.Version,
            RuntimeIdentifier = staged.Release.RuntimeIdentifier,
            Sha256 = staged.Release.Sha256,
            Signature = staged.Release.Signature
        };

        return await _commandService.SendAsync(new AgentCommandSendRequest
        {
            AgentId = agent.Id,
            CommandType = "AgentApplyUpgrade",
            Payload = JsonSerializer.Serialize(applyRequest, JsonOptions),
            Priority = 1,
            ExpirationMinutes = 120,
            MaxRetryCount = 1
        }, requestedBy, cancellationToken);
    }

    public async Task<(AgentRelease Release, byte[] PackageBytes)> GetReleasePackageAsync(int releaseId, CancellationToken cancellationToken)
    {
        if (!_currentAgentContext.IsAuthenticated || _currentAgentContext.AgentId <= 0 || _currentAgentContext.KurumId <= 0)
        {
            throw new BaseException("Agent doğrulanamadı.", 401);
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var agent = await db.Set<AgentEntity>()
            .FirstOrDefaultAsync(x => x.Id == _currentAgentContext.AgentId && !x.IsDeleted, cancellationToken)
            ?? throw new BaseException("Agent bulunamadı.", 404);

        if (agent.KurumId != _currentAgentContext.KurumId)
        {
            throw new BaseException("Agent kurum kapsamı doğrulanamadı.", 403);
        }

        if (agent.Durum != AgentDurum.Active)
        {
            throw new BaseException("Agent aktif değil.", 400);
        }

        var query = db.Set<AgentRelease>().Where(x => !x.IsDeleted && x.Enabled && x.Id == releaseId && x.KurumId == agent.KurumId);

        var release = await query.FirstOrDefaultAsync(cancellationToken)
            ?? throw new BaseException("Release bulunamadı.", 404);

        if (!string.Equals(AgentSemVer.NormalizeVersionText(release.ContractVersion), AgentSemVer.NormalizeVersionText(_compatibilityOptions.SupportedContractVersion), StringComparison.OrdinalIgnoreCase))
        {
            throw new BaseException("Release contract sürümü desteklenmiyor.", 400);
        }

        if (!string.Equals(release.RuntimeIdentifier?.Trim(), agent.RuntimeIdentifier?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new BaseException("Release runtime kimliği agent ile uyumsuz.", 400);
        }

        if (!AgentSemVer.TryParse(agent.AgentVersion, out var agentVersion))
        {
            throw new BaseException("Agent sürümü doğrulanamadı.", 400);
        }

        if (!AgentSemVer.TryParse(release.Version, out var releaseVersion))
        {
            throw new BaseException("Release sürümü doğrulanamadı.", 400);
        }

        if (releaseVersion.CompareTo(agentVersion) <= 0)
        {
            throw new BaseException("Release agent için yükseltme sürümü değil.", 400);
        }

        var stageCommand = await db.Set<AgentCommand>()
            .Where(x =>
                !x.IsDeleted
                && x.AgentId == agent.Id
                && x.ReleaseId == release.Id
                && x.CommandType == "AgentStageUpgrade"
                && (x.Status == AgentCommandStatus.Pending
                    || x.Status == AgentCommandStatus.Delivered
                    || x.Status == AgentCommandStatus.Accepted
                    || x.Status == AgentCommandStatus.Running))
            .SingleOrDefaultAsync(cancellationToken);

        if (stageCommand is null)
        {
            throw new BaseException("Geçerli staging komutu bulunamadı.", 404);
        }

        var stageRequest = JsonSerializer.Deserialize<AgentStageUpgradeRequest>(stageCommand.Payload ?? string.Empty, JsonOptions)
            ?? throw new BaseException("Stage komutu doğrulanamadı.", 409);

        if (!ReleaseMatchesRequest(release, stageRequest))
        {
            throw new BaseException("Stage komutu release kaydıyla eşleşmiyor.", 409);
        }

        if (!File.Exists(release.PackagePath))
        {
            throw new BaseException("Release paketi bulunamadı.", 404);
        }

        var packageBytes = await File.ReadAllBytesAsync(release.PackagePath, cancellationToken);
        var hash = Convert.ToHexString(SHA256.HashData(packageBytes));
        if (!string.Equals(hash, release.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new BaseException("Release paketi hash doğrulaması başarısız.", 409);
        }

        if (release.PackageSize > 0 && packageBytes.LongLength != release.PackageSize)
        {
            throw new BaseException("Release paketi boyutu doğrulanamadı.", 409);
        }

        return (release, packageBytes);
    }

    private static bool ReleaseMatchesRequest(AgentRelease release, AgentStageUpgradeRequest request) =>
        release.Id == request.ReleaseId
        && string.Equals(release.Version, request.Version, StringComparison.Ordinal)
        && string.Equals(release.ContractVersion, request.ContractVersion, StringComparison.Ordinal)
        && string.Equals(release.RuntimeIdentifier, request.RuntimeIdentifier, StringComparison.Ordinal)
        && string.Equals(release.Sha256, request.Sha256, StringComparison.OrdinalIgnoreCase)
        && release.PackageSize == request.PackageSize
        && release.PublishedAt == request.PublishedAt
        && string.Equals(release.ReleaseNotes ?? string.Empty, request.ReleaseNotes ?? string.Empty, StringComparison.Ordinal);

    private async Task<StagedReleaseContext?> GetStagedReleaseAsync(
        StysAppDbContext db,
        AgentEntity agent,
        CancellationToken cancellationToken)
    {
        var stageCommand = await db.Set<AgentCommand>()
            .Where(x =>
                !x.IsDeleted
                && x.AgentId == agent.Id
                && x.CommandType == "AgentStageUpgrade"
                && x.Status == AgentCommandStatus.Completed)
            .OrderByDescending(x => x.CompletedAt ?? x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (stageCommand is null || string.IsNullOrWhiteSpace(stageCommand.ResultPayload))
        {
            return null;
        }

        var stageResponse = JsonSerializer.Deserialize<AgentStageUpgradeResponse>(stageCommand.ResultPayload, JsonOptions)
            ?? throw new BaseException("Stage sonucu doğrulanamadı.", 409);

        if (stageResponse.StageStatus != AgentReleaseStageStatus.Staged)
        {
            return null;
        }

        var release = await db.Set<AgentRelease>()
            .FirstOrDefaultAsync(x => x.Id == stageResponse.ReleaseId && !x.IsDeleted && x.KurumId == agent.KurumId, cancellationToken)
            ?? throw new BaseException("Release bulunamadı.", 404);

        if (!string.Equals(release.Version, stageResponse.Version, StringComparison.Ordinal)
            || !string.Equals(release.RuntimeIdentifier, stageResponse.RuntimeIdentifier, StringComparison.OrdinalIgnoreCase))
        {
            throw new BaseException("Stage sonucu release kaydıyla eşleşmiyor.", 409);
        }

        return new StagedReleaseContext(release, stageResponse);
    }

    private sealed record StagedReleaseContext(AgentRelease Release, AgentStageUpgradeResponse StageResponse);

    private async Task<AgentRelease?> SelectBestReleaseAsync(StysAppDbContext db, AgentEntity agent, CancellationToken cancellationToken)
    {
        var runtimeIdentifier = agent.RuntimeIdentifier?.Trim();
        if (string.IsNullOrWhiteSpace(runtimeIdentifier))
        {
            throw new BaseException("Agent runtime kimliği bulunamadı.", 400);
        }

        if (!AgentSemVer.TryParse(agent.AgentVersion, out var currentVersion))
        {
            throw new BaseException("Agent sürümü doğrulanamadı.", 400);
        }

        var supportedContractVersion = AgentSemVer.NormalizeVersionText(_compatibilityOptions.SupportedContractVersion);
        if (string.IsNullOrWhiteSpace(supportedContractVersion))
        {
            throw new BaseException("Desteklenen contract sürümü yapılandırılmadı.", 500);
        }

        var candidates = await db.Set<AgentRelease>()
            .Where(x => !x.IsDeleted
                && x.Enabled
                && x.KurumId == agent.KurumId
                && x.RuntimeIdentifier == runtimeIdentifier
                && x.ContractVersion == supportedContractVersion)
            .ToListAsync(cancellationToken);

        var upgraded = new List<(AgentRelease Release, AgentSemanticVersion Version)>();
        foreach (var release in candidates)
        {
            if (string.IsNullOrWhiteSpace(release.PackagePath) || !File.Exists(release.PackagePath))
            {
                continue;
            }

            if (!AgentSemVer.TryParse(release.Version, out var targetVersion))
            {
                continue;
            }

            if (targetVersion.CompareTo(currentVersion) <= 0)
            {
                continue;
            }

            upgraded.Add((release, targetVersion));
        }

        var selected = upgraded
            .OrderByDescending(x => x.Version)
            .ThenByDescending(x => x.Release.PublishedAt)
            .Select(x => x.Release)
            .FirstOrDefault();

        return selected;
    }

    private void EnforceTenantAccess(int kurumId)
    {
        if (_tenantAccessor.IsSuperAdmin())
        {
            return;
        }

        if (!_tenantAccessor.GetAccessibleKurumIds().Contains(kurumId))
        {
            throw new BaseException("Bu kuruma erişim yetkiniz yok.", 403);
        }
    }
}
