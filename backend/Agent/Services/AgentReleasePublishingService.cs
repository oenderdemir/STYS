using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using STYS.Agent.Contracts.Dtos;
using STYS.Agent.Contracts.Versioning;
using STYS.Agent.Entities;
using STYS.Agent.Options;
using STYS.Infrastructure.EntityFramework;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Agent.Services;

public interface IAgentReleasePublishingService
{
    Task<IReadOnlyCollection<AgentReleaseDto>> GetAllAsync(CancellationToken cancellationToken);
    Task<AgentReleaseDto> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<AgentReleaseDto> PublishAsync(AgentReleasePublishRequest request, Stream package, CancellationToken cancellationToken);
    Task<AgentReleaseDto> SetEnabledAsync(int id, bool enabled, CancellationToken cancellationToken);
}

/// <summary>
/// Publishes signed agent releases. The manifest signature covers the release id, so the row must
/// exist before it can be signed; the whole publish therefore runs inside one transaction that
/// creates a disabled draft, signs it, moves the package into place, and only then applies the
/// caller's desired enabled state. A release is never visible to upgrade selection unsigned.
/// </summary>
public sealed class AgentReleasePublishingService : IAgentReleasePublishingService
{
    public const string SupportedRuntimeIdentifier = "win-x64";

    private readonly IDbContextFactory<StysAppDbContext> _dbContextFactory;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly IAgentReleaseSigner _signer;
    private readonly IAgentReleasePackageStorage _storage;
    private readonly AgentReleasePublishingOptions _publishingOptions;
    private readonly AgentCompatibilityOptions _compatibilityOptions;

    public AgentReleasePublishingService(
        IDbContextFactory<StysAppDbContext> dbContextFactory,
        ICurrentTenantAccessor tenantAccessor,
        IAgentReleaseSigner signer,
        IAgentReleasePackageStorage storage,
        IOptions<AgentReleasePublishingOptions>? publishingOptions = null,
        IOptions<AgentCompatibilityOptions>? compatibilityOptions = null)
    {
        _dbContextFactory = dbContextFactory;
        _tenantAccessor = tenantAccessor;
        _signer = signer;
        _storage = storage;
        _publishingOptions = publishingOptions?.Value ?? new AgentReleasePublishingOptions();
        _compatibilityOptions = compatibilityOptions?.Value ?? new AgentCompatibilityOptions();
    }

    public async Task<IReadOnlyCollection<AgentReleaseDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var query = db.Set<AgentRelease>().Where(x => !x.IsDeleted);
        query = ApplyTenantScope(query);

        var releases = await query
            .OrderByDescending(x => x.PublishedAt)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

        return releases.Select(ToDto).ToList();
    }

    public async Task<AgentReleaseDto> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var release = await LoadForTenantAsync(db, id, cancellationToken);
        return ToDto(release);
    }

    public async Task<AgentReleaseDto> PublishAsync(AgentReleasePublishRequest request, Stream package, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(package);

        var kurumId = ResolvePublishKurumId();
        var version = RequireVersion(request.Version, "Sürüm");
        var contractVersion = RequireVersion(request.ContractVersion, "Contract sürümü");
        var runtimeIdentifier = NormalizeRuntimeIdentifier(request.RuntimeIdentifier);

        var supportedContract = AgentSemVer.NormalizeVersionText(_compatibilityOptions.SupportedContractVersion);
        if (!string.Equals(contractVersion, supportedContract, StringComparison.OrdinalIgnoreCase))
        {
            throw new BaseException($"Contract sürümü desteklenmiyor. Beklenen: {supportedContract}", 400);
        }

        // Fail before touching disk when signing cannot succeed, so an unsignable upload does not
        // leave bytes behind.
        _ = _signer.ExportPublicKeyPem();

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureNotDuplicateAsync(db, kurumId, version, contractVersion, runtimeIdentifier, cancellationToken);

        var temp = await _storage.WriteTempAsync(package, _publishingOptions.MaxPackageSizeBytes, cancellationToken);
        string? finalPath = null;

        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            var release = new AgentRelease
            {
                KurumId = kurumId,
                Version = version,
                ContractVersion = contractVersion,
                RuntimeIdentifier = runtimeIdentifier,
                Sha256 = temp.Sha256,
                PackageSize = temp.Length,
                PublishedAt = DateTimeOffset.UtcNow,
                ReleaseNotes = string.IsNullOrWhiteSpace(request.ReleaseNotes) ? null : request.ReleaseNotes.Trim(),
                Enabled = false,
                Signature = string.Empty,
                PackagePath = string.Empty
            };

            db.Set<AgentRelease>().Add(release);
            await db.SaveChangesAsync(cancellationToken);

            // ReleaseId is part of the canonical manifest, so signing can only happen now.
            var payload = AgentReleaseManifest.BuildSignaturePayload(
                release.Id,
                release.Version,
                release.ContractVersion,
                release.RuntimeIdentifier,
                release.Sha256,
                release.PackageSize,
                release.PublishedAt);

            release.Signature = _signer.SignManifest(payload);

            finalPath = _storage.MoveToFinal(temp, kurumId, release.Id, version, runtimeIdentifier);
            release.PackagePath = finalPath;
            release.Enabled = request.Enabled;

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ToDto(release);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Two concurrent publishes of the same version: the database index is the arbiter.
            _storage.TryDelete(finalPath);
            _storage.TryDelete(temp.Path);
            throw new BaseException("Bu sürüm bu kurum ve runtime için zaten yayınlanmış.", 409);
        }
        catch
        {
            _storage.TryDelete(finalPath);
            _storage.TryDelete(temp.Path);
            throw;
        }
    }

    public async Task<AgentReleaseDto> SetEnabledAsync(int id, bool enabled, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var release = await LoadForTenantAsync(db, id, cancellationToken);

        if (enabled && string.IsNullOrWhiteSpace(release.Signature))
        {
            throw new BaseException("İmzasız release aktifleştirilemez.", 409);
        }

        if (enabled && (string.IsNullOrWhiteSpace(release.PackagePath) || !File.Exists(release.PackagePath)))
        {
            throw new BaseException("Release paketi bulunamadı, aktifleştirilemez.", 409);
        }

        release.Enabled = enabled;
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(release);
    }

    private async Task EnsureNotDuplicateAsync(
        StysAppDbContext db,
        int kurumId,
        string version,
        string contractVersion,
        string runtimeIdentifier,
        CancellationToken cancellationToken)
    {
        var exists = await db.Set<AgentRelease>().AnyAsync(
            x => !x.IsDeleted
                && x.KurumId == kurumId
                && x.RuntimeIdentifier == runtimeIdentifier
                && x.Version == version
                && x.ContractVersion == contractVersion,
            cancellationToken);

        if (exists)
        {
            throw new BaseException("Bu sürüm bu kurum ve runtime için zaten yayınlanmış.", 409);
        }
    }

    private async Task<AgentRelease> LoadForTenantAsync(StysAppDbContext db, int id, CancellationToken cancellationToken)
    {
        var release = await db.Set<AgentRelease>().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken)
            ?? throw new BaseException("Release bulunamadı.", 404);

        EnforceTenantAccess(release.KurumId);
        return release;
    }

    private IQueryable<AgentRelease> ApplyTenantScope(IQueryable<AgentRelease> query)
    {
        if (_tenantAccessor.IsSuperAdmin())
        {
            return query;
        }

        var accessible = _tenantAccessor.GetAccessibleKurumIds().ToArray();
        return query.Where(x => accessible.Contains(x.KurumId));
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

    /// <summary>
    /// Publishing targets exactly one kurum, so a caller with access to several must not have one
    /// picked arbitrarily on their behalf, and the value is never taken from the request body.
    /// </summary>
    private int ResolvePublishKurumId()
    {
        var kurumId = _tenantAccessor.GetCurrentKurumId();
        if (kurumId is > 0)
        {
            return kurumId.Value;
        }

        var accessible = _tenantAccessor.GetAccessibleKurumIds().ToArray();
        if (accessible.Length == 1)
        {
            return accessible[0];
        }

        throw new BaseException("Release yayınlamak için kurum bağlamı belirlenemedi.", 400);
    }

    private static string RequireVersion(string? value, string label)
    {
        var normalized = AgentSemVer.NormalizeVersionText(value);
        if (string.IsNullOrWhiteSpace(normalized) || !AgentSemVer.TryParse(normalized, out _))
        {
            throw new BaseException($"{label} geçerli bir sürüm değil.", 400);
        }

        return normalized;
    }

    private static string NormalizeRuntimeIdentifier(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        if (!string.Equals(normalized, SupportedRuntimeIdentifier, StringComparison.Ordinal))
        {
            throw new BaseException($"Bu aşamada yalnız {SupportedRuntimeIdentifier} release desteklenir.", 400);
        }

        return normalized;
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is Microsoft.Data.SqlClient.SqlException sql && sql.Number is 2601 or 2627;

    private static AgentReleaseDto ToDto(AgentRelease release) => new()
    {
        Id = release.Id,
        Version = release.Version,
        ContractVersion = release.ContractVersion,
        RuntimeIdentifier = release.RuntimeIdentifier,
        Sha256 = release.Sha256,
        Signature = release.Signature,
        PackageSize = release.PackageSize,
        PublishedAt = release.PublishedAt,
        Enabled = release.Enabled,
        ReleaseNotes = release.ReleaseNotes
    };
}
