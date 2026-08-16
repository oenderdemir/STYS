using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using STYS.Agent.Authorization;
using STYS.Agent.Contracts.Dtos;
using STYS.Agent.Contracts.Enums;
using STYS.Agent.Entities;
using STYS.Infrastructure.EntityFramework;
using STYS.Tesisler.Entities;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Agent.Services;

public sealed class AgentInstallationSessionService : IAgentInstallationSessionService
{
    private static readonly HashSet<string> SupportedRids = new(StringComparer.OrdinalIgnoreCase)
    {
        "win-x64",
        "linux-x64"
    };

    private static readonly HashSet<string> AllowedScopes = new(StringComparer.OrdinalIgnoreCase)
    {
        AgentPolicies.AgentHeartbeat,
        AgentPolicies.AgentCommandRead,
        AgentPolicies.AgentCommandExecute,
        AgentPolicies.AgentResultWrite,
        AgentPolicies.AgentConfigRead
    };

    private readonly IDbContextFactory<StysAppDbContext> _dbContextFactory;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public AgentInstallationSessionService(
        IDbContextFactory<StysAppDbContext> dbContextFactory,
        ICurrentTenantAccessor tenantAccessor)
    {
        _dbContextFactory = dbContextFactory;
        _tenantAccessor = tenantAccessor;
    }

    public async Task<IReadOnlyCollection<AgentInstallationSessionDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var kurumId = RequireCurrentKurumId();
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var sessions = await db.Set<AgentInstallationSession>()
            .AsNoTracking()
            .Where(x => !x.IsDeleted && x.KurumId == kurumId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return await MapAsync(db, sessions, cancellationToken);
    }

    public async Task<AgentInstallationSessionDto> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var kurumId = RequireCurrentKurumId();
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var session = await db.Set<AgentInstallationSession>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted && x.KurumId == kurumId, cancellationToken);

        if (session is null)
            throw new BaseException("Kurulum oturumu bulunamadı.", 404);

        return (await MapAsync(db, [session], cancellationToken)).Single();
    }

    public async Task<AgentInstallationSessionCreateResponse> CreateAsync(AgentInstallationSessionCreateRequest request, string createdBy, CancellationToken cancellationToken)
    {
        var kurumId = RequireCurrentKurumId();
        ValidateRequest(request);
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        db.AllowExplicitTenantWritesWithoutAmbientTenant = true;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var tesis = await db.Set<Tesis>()
            .FirstOrDefaultAsync(x => x.Id == request.TesisId && x.KurumId == kurumId && !x.IsDeleted, cancellationToken);
        if (tesis is null)
            throw new BaseException("Seçilen tesis mevcut kurum kapsamında değil.", 400);

        var normalizedScopes = NormalizeScopes(request.Scopes);
        var expiresAt = DateTime.UtcNow.AddHours(Math.Clamp(request.ExpirationHours ?? 24, 1, 72));

        var session = new AgentInstallationSession
        {
            KurumId = kurumId,
            TesisId = request.TesisId,
            AgentDisplayName = request.AgentDisplayName.Trim(),
            TargetRid = NormalizeRid(request.TargetRid),
            Scopes = JsonSerializer.Serialize(normalizedScopes),
            Status = AgentInstallationSessionStatus.EnrollmentPending,
            ExpiresAt = expiresAt,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow
        };

        db.Set<AgentInstallationSession>().Add(session);
        await db.SaveChangesAsync(cancellationToken);

        var enrollmentCode = GenerateSecureCode();
        var enrollment = new AgentEnrollment
        {
            // Hash only: the plaintext is returned once below, in the create response.
            CodeHash = AgentEnrollmentCodeHasher.Hash(enrollmentCode),
            CodePrefix = AgentEnrollmentCodeHasher.BuildPrefix(enrollmentCode),
            KurumId = kurumId,
            TesisIds = JsonSerializer.Serialize(new[] { request.TesisId }),
            AllowedScopes = JsonSerializer.Serialize(normalizedScopes),
            MaxKullanimSayisi = 1,
            RequiresApproval = request.RequiresApproval,
            ExpiresAt = expiresAt,
            Durum = AgentEnrollmentDurum.Active,
            AgentInstallationSessionId = session.Id,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow
        };

        db.Set<AgentEnrollment>().Add(enrollment);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new AgentInstallationSessionCreateResponse
        {
            EnrollmentCode = enrollmentCode,
            Session = MapToDto(session, tesis.Ad, enrollment.Id)
        };
    }

    public async Task CancelAsync(int id, string cancelledBy, CancellationToken cancellationToken)
    {
        var kurumId = RequireCurrentKurumId();
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        db.AllowExplicitTenantWritesWithoutAmbientTenant = true;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var session = await db.Set<AgentInstallationSession>()
            .Include(x => x.Enrollment)
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted && x.KurumId == kurumId, cancellationToken);

        if (session is null)
            throw new BaseException("Kurulum oturumu bulunamadı.", 404);

        if (IsTerminal(session.Status))
            throw new BaseException("Terminal durumdaki kurulum oturumu iptal edilemez.", 400);

        session.Status = AgentInstallationSessionStatus.Cancelled;
        session.CancelledAt = DateTime.UtcNow;
        session.UpdatedBy = cancelledBy;
        session.UpdatedAt = DateTime.UtcNow;

        if (session.Enrollment is not null && session.Enrollment.Durum == AgentEnrollmentDurum.Active)
            session.Enrollment.Durum = AgentEnrollmentDurum.Revoked;

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task MarkOnlineFromHeartbeatAsync(int agentId, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        db.AllowExplicitTenantWritesWithoutAmbientTenant = true;

        var session = await db.Set<AgentInstallationSession>()
            .FirstOrDefaultAsync(x =>
                !x.IsDeleted
                && x.EnrolledAgentId == agentId
                && x.Status == AgentInstallationSessionStatus.Enrolled,
                cancellationToken);

        if (session is null)
        {
            return;
        }

        session.Status = AgentInstallationSessionStatus.Online;
        session.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<(string FileName, string ContentType, byte[] Content)> GetPackageAsync(int id, string baseUrl, CancellationToken cancellationToken)
    {
        var kurumId = RequireCurrentKurumId();
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        db.AllowExplicitTenantWritesWithoutAmbientTenant = true;

        var session = await db.Set<AgentInstallationSession>()
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted && x.KurumId == kurumId, cancellationToken);

        if (session is null)
            throw new BaseException("Kurulum oturumu bulunamadı.", 404);

        if (string.IsNullOrWhiteSpace(session.TargetRid))
            throw new BaseException("Kurulum oturumu RID bilgisi eksik.", 400);

        var packageBytes = AgentInstallerPackageBuilder.Build(session, baseUrl);

        if (session.Status is AgentInstallationSessionStatus.Created or AgentInstallationSessionStatus.EnrollmentPending)
        {
            session.Status = AgentInstallationSessionStatus.PackageReady;
            session.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        return (
            FileName: $"stys-agent-install-{session.TargetRid}-{session.Id}.zip",
            ContentType: "application/zip",
            Content: packageBytes);
    }

    private async Task<IReadOnlyCollection<AgentInstallationSessionDto>> MapAsync(StysAppDbContext db, IReadOnlyCollection<AgentInstallationSession> sessions, CancellationToken cancellationToken)
    {
        var tesisIds = sessions.Select(x => x.TesisId).Distinct().ToList();
        var tesisAdById = await db.Set<Tesis>()
            .AsNoTracking()
            .Where(x => tesisIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Ad })
            .ToDictionaryAsync(x => x.Id, x => x.Ad, cancellationToken);

        var sessionIds = sessions.Select(x => x.Id).ToList();
        var enrollmentBySessionId = sessionIds.Count == 0
            ? new Dictionary<int, AgentEnrollment>()
            : await db.Set<AgentEnrollment>()
                .AsNoTracking()
                .Where(x => x.AgentInstallationSessionId != null && sessionIds.Contains(x.AgentInstallationSessionId.Value))
                .ToDictionaryAsync(x => x.AgentInstallationSessionId!.Value, cancellationToken);

        return sessions.Select(x =>
        {
            enrollmentBySessionId.TryGetValue(x.Id, out var enrollment);
            tesisAdById.TryGetValue(x.TesisId, out var tesisAd);
            return MapToDto(x, tesisAd, enrollment?.Id);
        }).ToList();
    }

    private AgentInstallationSessionDto MapToDto(AgentInstallationSession session, string? tesisAd, int? enrollmentId)
    {
        return new AgentInstallationSessionDto
        {
            Id = session.Id,
            KurumId = session.KurumId,
            TesisId = session.TesisId,
            TesisAd = tesisAd,
            AgentDisplayName = session.AgentDisplayName,
            TargetRid = session.TargetRid,
            Scopes = JsonSerializer.Deserialize<List<string>>(session.Scopes) ?? [],
            Status = session.Status,
            EnrollmentId = enrollmentId,
            EnrolledAgentId = session.EnrolledAgentId,
            ExpiresAt = session.ExpiresAt,
            CompletedAt = session.CompletedAt,
            CancelledAt = session.CancelledAt,
            CreatedAt = session.CreatedAt ?? DateTime.MinValue,
            UpdatedAt = session.UpdatedAt
        };
    }

    private static bool IsTerminal(AgentInstallationSessionStatus status) =>
        status is AgentInstallationSessionStatus.Completed
            or AgentInstallationSessionStatus.Expired
            or AgentInstallationSessionStatus.Cancelled
            or AgentInstallationSessionStatus.Failed;

    private static void ValidateRequest(AgentInstallationSessionCreateRequest request)
    {
        if (request is null)
            throw new BaseException("İstek zorunludur.", 400);
        if (request.TesisId <= 0)
            throw new BaseException("Geçerli bir tesis seçilmelidir.", 400);
        if (string.IsNullOrWhiteSpace(request.AgentDisplayName))
            throw new BaseException("Agent görüntü adı zorunludur.", 400);
        if (string.IsNullOrWhiteSpace(request.TargetRid))
            throw new BaseException("Hedef RID zorunludur.", 400);
        if (request.Scopes.Count == 0)
            throw new BaseException("En az bir scope seçilmelidir.", 400);
    }

    private static IReadOnlyCollection<string> NormalizeScopes(IReadOnlyCollection<string> scopes)
    {
        var normalized = scopes
            .Select(NormalizeScope)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalized.Count == 0)
            throw new BaseException("En az bir scope seçilmelidir.", 400);

        var invalid = normalized.Where(x => !AllowedScopes.Contains(x)).ToList();
        if (invalid.Count > 0)
            throw new BaseException($"Geçersiz scope(lar): {string.Join(", ", invalid)}", 400);

        return normalized;
    }

    private static string NormalizeScope(string scope) => scope.Trim().ToLowerInvariant();

    private static string NormalizeRid(string rid)
    {
        var normalized = rid.Trim().ToLowerInvariant();
        if (!SupportedRids.Contains(normalized))
            throw new BaseException($"Desteklenmeyen RID: {rid}", 400);
        return normalized;
    }

    private int RequireCurrentKurumId()
    {
        var kurumId = _tenantAccessor.GetCurrentKurumId();
        if (kurumId.HasValue)
            return kurumId.Value;
        if (_tenantAccessor.IsSuperAdmin())
            throw new BaseException("Kurulum oturumu oluşturmak için aktif kurum seçilmelidir.", 400);
        throw new BaseException("Aktif kurum seçilmedi.", 400);
    }

    private static string GenerateSecureCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var bytes = RandomNumberGenerator.GetBytes(16);
        var code = new char[16];
        for (var i = 0; i < 16; i++)
            code[i] = chars[bytes[i] % chars.Length];
        return new string(code);
    }

}
