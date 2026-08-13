using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using STYS.Agent.Contracts.Dtos;
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
    private readonly AgentCommandService _commandService;
    private readonly AgentCompatibilityOptions _compatibilityOptions;

    public AgentReleaseService(
        IDbContextFactory<StysAppDbContext> dbContextFactory,
        ICurrentTenantAccessor tenantAccessor,
        AgentCommandService commandService,
        IOptions<AgentCompatibilityOptions>? compatibilityOptions = null)
    {
        _dbContextFactory = dbContextFactory;
        _tenantAccessor = tenantAccessor;
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

    public async Task<(AgentRelease Release, byte[] PackageBytes)> GetReleasePackageAsync(string version, string runtimeIdentifier, CancellationToken cancellationToken)
    {
        var agentContext = _tenantAccessor.IsSuperAdmin() ? null : _tenantAccessor.GetCurrentKurumId();
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var query = db.Set<AgentRelease>().Where(x => !x.IsDeleted && x.Enabled);
        if (agentContext.HasValue)
        {
            query = query.Where(x => x.KurumId == agentContext.Value);
        }

        query = query.Where(x =>
            x.RuntimeIdentifier == runtimeIdentifier
            && x.Version == version);

        var release = await query.FirstOrDefaultAsync(cancellationToken)
            ?? throw new BaseException("Release bulunamadı.", 404);

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
