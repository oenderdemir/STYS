using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using STYS.Agent.Contracts.Dtos;
using STYS.Agent.Contracts.Enums;
using STYS.Agent.Entities;
using STYS.Agent.Options;
using STYS.Infrastructure.EntityFramework;
using STYS.Tesisler.Entities;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;
using AgentEntity = STYS.Agent.Entities.Agent;

namespace STYS.Agent.Services;

public sealed class AgentService : IAgentService
{
    private readonly IDbContextFactory<StysAppDbContext> _dbContextFactory;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly AgentCompatibilityOptions _compatibilityOptions;

    public AgentService(
        IDbContextFactory<StysAppDbContext> dbContextFactory,
        ICurrentTenantAccessor tenantAccessor,
        IOptions<AgentCompatibilityOptions>? compatibilityOptions = null)
    {
        _dbContextFactory = dbContextFactory;
        _tenantAccessor = tenantAccessor;
        _compatibilityOptions = compatibilityOptions?.Value ?? new AgentCompatibilityOptions();
    }

    public async Task<AgentDto> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var agent = await db.Set<AgentEntity>()
            .Include(x => x.Tesisler)
            .Include(x => x.Scopes)
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
        if (agent is null)
            throw new BaseException("Agent bulunamadı.", 404);

        EnforceKurumAccess(agent);
        return MapToDto(agent);
    }

    public async Task<IReadOnlyCollection<AgentListDto>> GetAllAsync(int? kurumId, int? tesisId, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Set<AgentEntity>().Where(x => !x.IsDeleted);

        query = ApplyKurumFilter(query, kurumId);
        if (tesisId.HasValue && tesisId.Value > 0)
        {
            query = query.Where(x => x.Tesisler.Any(t => !t.IsDeleted && t.TesisId == tesisId.Value));
        }

        var agents = await query
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return agents.Select(MapToListDto).ToList();
    }

    public async Task<AgentDto> CreateAsync(AgentKaydetRequest request, string createdBy, CancellationToken cancellationToken)
    {
        var kurumId = _tenantAccessor.GetCurrentKurumId();
        if (!kurumId.HasValue) throw new BaseException("Aktif kurum seçilmedi.", 400);
        EnforceKurumAccess(kurumId.Value);
        await ValidateTesislerAsync(kurumId.Value, request.TesisIds, cancellationToken);

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var agentKey = $"AGNT-{Guid.NewGuid():N}"[..16];
        var agent = new AgentEntity
        {
            Ad = request.Ad,
            AgentKey = agentKey,
            KurumId = kurumId.Value,
            Durum = AgentDurum.Active,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow
        };
        db.Set<AgentEntity>().Add(agent);
        await db.SaveChangesAsync(cancellationToken);

        foreach (var tesisId in request.TesisIds)
        {
            db.Set<AgentTesis>().Add(new AgentTesis { AgentId = agent.Id, KurumId = kurumId.Value, TesisId = tesisId, CreatedBy = createdBy, CreatedAt = DateTime.UtcNow });
        }

        foreach (var scope in request.Scopes)
        {
            db.Set<AgentScope>().Add(new AgentScope { AgentId = agent.Id, KurumId = kurumId.Value, Scope = scope.ToLowerInvariant().Trim(), CreatedBy = createdBy, CreatedAt = DateTime.UtcNow });
        }

        await db.SaveChangesAsync(cancellationToken);
        return MapToDto(agent);
    }

    public async Task<AgentDto> UpdateAsync(int id, AgentKaydetRequest request, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var agent = await db.Set<AgentEntity>().Include(x => x.Tesisler).Include(x => x.Scopes).Include(x => x.Credentialler).FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
        if (agent is null) throw new BaseException("Agent bulunamadı.", 404);
        EnforceKurumAccess(agent);

        await ValidateTesislerAsync(agent.KurumId, request.TesisIds, cancellationToken);

        agent.Ad = request.Ad;
        var existing = agent.Tesisler.Where(x => !x.IsDeleted).Select(x => x.TesisId).ToHashSet();
        foreach (var tesisId in request.TesisIds.Where(x => !existing.Contains(x)))
            db.Set<AgentTesis>().Add(new AgentTesis { AgentId = agent.Id, KurumId = agent.KurumId, TesisId = tesisId, CreatedBy = agent.UpdatedBy, CreatedAt = DateTime.UtcNow });
        foreach (var t in agent.Tesisler.Where(x => !request.TesisIds.Contains(x.TesisId)))
            t.IsDeleted = true;

        var scopeChanged = SyncScopes(db, agent, request.Scopes);

        await db.SaveChangesAsync(cancellationToken);

        if (scopeChanged)
            IncrementCredentialVersions(agent);

        await db.SaveChangesAsync(cancellationToken);
        return MapToDto(agent);
    }

    public async Task UpdateScopesAsync(int id, IReadOnlyCollection<string> scopes, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var agent = await db.Set<AgentEntity>().Include(x => x.Scopes).Include(x => x.Credentialler).FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
        if (agent is null) throw new BaseException("Agent bulunamadı.", 404);
        EnforceKurumAccess(agent);

        var scopeChanged = SyncScopes(db, agent, scopes);

        await db.SaveChangesAsync(cancellationToken);

        if (scopeChanged)
        {
            IncrementCredentialVersions(agent);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task ApproveAsync(int id, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var agent = await db.Set<AgentEntity>()
            .Include(x => x.Enrollments)
            .ThenInclude(x => x.InstallationSession)
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
        if (agent is null) throw new BaseException("Agent bulunamadı.", 404);
        EnforceKurumAccess(agent);
        if (agent.Durum != AgentDurum.PendingApproval) throw new BaseException("Sadece onay bekleyen agent onaylanabilir.", 400);
        agent.Durum = AgentDurum.Active;

        foreach (var session in agent.Enrollments
                     .Where(x => !x.IsDeleted && x.InstallationSession is not null && x.InstallationSession.Status == AgentInstallationSessionStatus.PendingApproval)
                     .Select(x => x.InstallationSession!))
        {
            session.Status = AgentInstallationSessionStatus.Enrolled;
            session.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RejectAsync(int id, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var agent = await db.Set<AgentEntity>()
            .Include(x => x.Credentialler)
            .Include(x => x.Enrollments)
            .ThenInclude(x => x.InstallationSession)
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
        if (agent is null) throw new BaseException("Agent bulunamadı.", 404);
        EnforceKurumAccess(agent);
        if (agent.Durum != AgentDurum.PendingApproval) throw new BaseException("Sadece onay bekleyen agent reddedilebilir.", 400);

        agent.Durum = AgentDurum.Rejected;
        // The credential the agent already stored locally must stop working immediately; it can
        // never be exchanged for a token again.
        foreach (var cred in agent.Credentialler.Where(x => x.AktifMi))
        {
            cred.AktifMi = false;
            cred.CredentialVersion++;
            cred.RevokedAt = DateTime.UtcNow;
        }

        foreach (var session in agent.Enrollments
                     .Where(x => !x.IsDeleted && x.InstallationSession is not null && x.InstallationSession.Status == AgentInstallationSessionStatus.PendingApproval)
                     .Select(x => x.InstallationSession!))
        {
            session.Status = AgentInstallationSessionStatus.Failed;
            session.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DisableAsync(int id, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var agent = await db.Set<AgentEntity>().Include(x => x.Credentialler).FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
        if (agent is null) throw new BaseException("Agent bulunamadı.", 404);
        EnforceKurumAccess(agent);

        agent.Durum = AgentDurum.Disabled;
        foreach (var cred in agent.Credentialler.Where(x => x.AktifMi)) { cred.AktifMi = false; cred.CredentialVersion++; cred.RevokedAt = DateTime.UtcNow; }
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeAsync(int id, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var agent = await db.Set<AgentEntity>().Include(x => x.Credentialler).FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
        if (agent is null) throw new BaseException("Agent bulunamadı.", 404);
        EnforceKurumAccess(agent);

        agent.Durum = AgentDurum.Revoked;
        foreach (var cred in agent.Credentialler) { cred.AktifMi = false; cred.CredentialVersion++; cred.RevokedAt = DateTime.UtcNow; }
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<AgentEnrollmentCodeDto> GenerateEnrollmentCodeAsync(AgentEnrollmentCodeRequest request, string createdBy, CancellationToken cancellationToken)
    {
        var kurumId = _tenantAccessor.GetCurrentKurumId();
        if (!kurumId.HasValue) throw new BaseException("Aktif kurum seçilmedi.", 400);
        EnforceKurumAccess(kurumId.Value);
        await ValidateTesislerAsync(kurumId.Value, request.TesisIds, cancellationToken);

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var code = GenerateSecureCode();
        var enrollment = new AgentEnrollment
        {
            // Only the hash is persisted; `code` below is the sole time the plaintext leaves here.
            CodeHash = AgentEnrollmentCodeHasher.Hash(code),
            CodePrefix = AgentEnrollmentCodeHasher.BuildPrefix(code),
            KurumId = kurumId.Value,
            TesisIds = System.Text.Json.JsonSerializer.Serialize(request.TesisIds),
            AllowedScopes = System.Text.Json.JsonSerializer.Serialize(request.AllowedScopes),
            MaxKullanimSayisi = request.MaxKullanimSayisi ?? 1,
            RequiresApproval = request.RequiresApproval,
            ExpiresAt = DateTime.UtcNow.AddHours(request.ExpirationHours ?? 24),
            Durum = AgentEnrollmentDurum.Active, CreatedBy = createdBy, CreatedAt = DateTime.UtcNow
        };
        db.Set<AgentEnrollment>().Add(enrollment);
        await db.SaveChangesAsync(cancellationToken);

        // This is the only response that ever carries the plaintext code.
        return new AgentEnrollmentCodeDto { Id = enrollment.Id, Code = code, CodePrefix = enrollment.CodePrefix, KurumId = enrollment.KurumId, TesisIds = request.TesisIds, AllowedScopes = request.AllowedScopes, RequiresApproval = request.RequiresApproval, MaxKullanimSayisi = enrollment.MaxKullanimSayisi, ExpiresAt = enrollment.ExpiresAt, Durum = (int)enrollment.Durum, CreatedAt = enrollment.CreatedAt ?? DateTime.UtcNow };
    }

    public async Task<IReadOnlyCollection<AgentEnrollmentCodeDto>> GetEnrollmentCodesAsync(int? kurumId, int? tesisId, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Set<AgentEnrollment>().Where(x => !x.IsDeleted);
        query = ApplyKurumFilter(query, kurumId);

        var codes = await query.OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken);
        var mapped = codes.Select(MapEnrollmentToDto);
        if (tesisId.HasValue && tesisId.Value > 0)
        {
            mapped = mapped.Where(x => x.TesisIds.Contains(tesisId.Value));
        }

        return mapped.ToList();
    }

    public async Task RevokeEnrollmentCodeAsync(int enrollmentId, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var enrollment = await db.Set<AgentEnrollment>().FirstOrDefaultAsync(x => x.Id == enrollmentId && !x.IsDeleted, cancellationToken);
        if (enrollment is null) throw new BaseException("Enrollment kodu bulunamadı.", 404);
        EnforceKurumAccess(enrollment.KurumId);
        enrollment.Durum = AgentEnrollmentDurum.Revoked;
        await db.SaveChangesAsync(cancellationToken);
    }

    private void EnforceKurumAccess(AgentEntity agent) => EnforceKurumAccess(agent.KurumId);
    private void EnforceKurumAccess(int targetKurumId)
    {
        if (_tenantAccessor.IsSuperAdmin()) return;
        var accessible = _tenantAccessor.GetAccessibleKurumIds();
        if (!accessible.Contains(targetKurumId)) throw new BaseException("Bu kuruma erişim yetkiniz yok.", 403);
    }

    private IQueryable<AgentEntity> ApplyKurumFilter(IQueryable<AgentEntity> query, int? kurumId)
    {
        if (kurumId.HasValue && kurumId.Value > 0)
        {
            EnforceKurumAccess(kurumId.Value);
            return query.Where(x => x.KurumId == kurumId.Value);
        }

        if (_tenantAccessor.IsSuperAdmin()) return query;
        var ids = _tenantAccessor.GetAccessibleKurumIds();
        return query.Where(x => ids.Contains(x.KurumId));
    }

    private IQueryable<AgentEnrollment> ApplyKurumFilter(IQueryable<AgentEnrollment> query, int? kurumId)
    {
        if (kurumId.HasValue && kurumId.Value > 0)
        {
            EnforceKurumAccess(kurumId.Value);
            return query.Where(x => x.KurumId == kurumId.Value);
        }

        if (_tenantAccessor.IsSuperAdmin()) return query;
        var ids = _tenantAccessor.GetAccessibleKurumIds();
        return query.Where(x => ids.Contains(x.KurumId));
    }

    private async Task ValidateTesislerAsync(int kurumId, IReadOnlyCollection<int> tesisIds, CancellationToken cancellationToken)
    {
        if (tesisIds.Count == 0) return;
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var validIds = await db.Set<Tesis>().Where(x => tesisIds.Contains(x.Id) && x.KurumId == kurumId && !x.IsDeleted).Select(x => x.Id).ToListAsync(cancellationToken);
        var invalid = tesisIds.Where(x => !validIds.Contains(x)).ToList();
        if (invalid.Count > 0) throw new BaseException($"Geçersiz tesis ID'leri: {string.Join(", ", invalid)}", 400);
    }

    private AgentDto MapToDto(AgentEntity agent)
    {
        var compatibility = AgentCompatibilityEvaluator.Evaluate(agent.AgentVersion, agent.ContractVersion, _compatibilityOptions);
        return new AgentDto
        {
            Id = agent.Id,
            Ad = agent.Ad,
            AgentKey = agent.AgentKey,
            KurumId = agent.KurumId,
            Durum = (int)agent.Durum,
            AgentVersion = agent.AgentVersion,
            ContractVersion = agent.ContractVersion,
            RuntimeIdentifier = agent.RuntimeIdentifier,
            MinimumSupportedAgentVersion = compatibility.MinimumSupportedAgentVersion,
            RecommendedAgentVersion = compatibility.RecommendedAgentVersion,
            SupportedContractVersion = compatibility.SupportedContractVersion,
            CompatibilityStatus = compatibility.CompatibilityStatus,
            LastHeartbeatAt = agent.LastHeartbeatAt,
            OnlineMi = ComputeOnline(agent.LastHeartbeatAt),
            CihazKimligi = agent.CihazKimligi,
            TesisIds = agent.Tesisler?.Where(x => !x.IsDeleted).Select(x => x.TesisId).ToList() ?? [],
            Scopes = agent.Scopes?.Where(x => !x.IsDeleted && x.AktifMi).Select(x => x.Scope).ToList() ?? [],
            CreatedAt = agent.CreatedAt ?? DateTime.MinValue
        };
    }

    private AgentListDto MapToListDto(AgentEntity agent)
    {
        var compatibility = AgentCompatibilityEvaluator.Evaluate(agent.AgentVersion, agent.ContractVersion, _compatibilityOptions);
        return new AgentListDto
        {
            Id = agent.Id,
            Ad = agent.Ad,
            AgentKey = agent.AgentKey,
            KurumId = agent.KurumId,
            Durum = (int)agent.Durum,
            AgentVersion = agent.AgentVersion,
            ContractVersion = agent.ContractVersion,
            RuntimeIdentifier = agent.RuntimeIdentifier,
            MinimumSupportedAgentVersion = compatibility.MinimumSupportedAgentVersion,
            RecommendedAgentVersion = compatibility.RecommendedAgentVersion,
            SupportedContractVersion = compatibility.SupportedContractVersion,
            CompatibilityStatus = compatibility.CompatibilityStatus,
            LastHeartbeatAt = agent.LastHeartbeatAt,
            OnlineMi = ComputeOnline(agent.LastHeartbeatAt),
            CreatedAt = agent.CreatedAt ?? DateTime.MinValue
        };
    }

    private static AgentEnrollmentCodeDto MapEnrollmentToDto(AgentEnrollment x)
    {
        // Code stays null here on purpose: the plaintext is unrecoverable after generation.
        return new AgentEnrollmentCodeDto { Id = x.Id, Code = null, CodePrefix = x.CodePrefix, KurumId = x.KurumId, TesisIds = System.Text.Json.JsonSerializer.Deserialize<List<int>>(x.TesisIds) ?? [], AllowedScopes = System.Text.Json.JsonSerializer.Deserialize<List<string>>(x.AllowedScopes) ?? [], RequiresApproval = x.RequiresApproval, KullanimSayisi = x.KullanimSayisi, MaxKullanimSayisi = x.MaxKullanimSayisi, ExpiresAt = x.ExpiresAt, Durum = (int)x.Durum, AgentId = x.AgentId, CreatedAt = x.CreatedAt ?? DateTime.MinValue };
    }

    private static readonly TimeSpan OnlineHeartbeatTolerance = TimeSpan.FromSeconds(90);

    private static bool ComputeOnline(DateTime? lastHeartbeat) =>
        lastHeartbeat.HasValue && (DateTime.UtcNow - lastHeartbeat.Value) <= OnlineHeartbeatTolerance;

    private static bool SyncScopes(StysAppDbContext db, AgentEntity agent, IReadOnlyCollection<string> requestedScopes)
    {
        var changed = false;
        var normalized = requestedScopes.Select(x => x.Trim().ToLowerInvariant()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var deleted = agent.Scopes?.Where(x => !x.IsDeleted).ToList() ?? new List<AgentScope>();

        foreach (var existing in deleted)
        {
            if (normalized.Contains(existing.Scope, StringComparer.OrdinalIgnoreCase))
            {
                if (!existing.AktifMi) { existing.AktifMi = true; changed = true; }
                normalized.Remove(existing.Scope);
            }
            else
            {
                existing.AktifMi = false;
                existing.IsDeleted = true;
                changed = true;
            }
        }

        foreach (var scope in normalized)
        {
            db.Set<AgentScope>().Add(new AgentScope { AgentId = agent.Id, KurumId = agent.KurumId, Scope = scope, AktifMi = true, CreatedBy = agent.UpdatedBy, CreatedAt = DateTime.UtcNow });
            changed = true;
        }

        return changed;
    }

    private static void IncrementCredentialVersions(AgentEntity agent)
    {
        foreach (var cred in agent.Credentialler?.Where(x => x.AktifMi) ?? [])
            cred.CredentialVersion++;
    }

    private static string GenerateSecureCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var bytes = RandomNumberGenerator.GetBytes(16);
        var code = new char[16];
        for (var i = 0; i < 16; i++) code[i] = chars[bytes[i] % chars.Length];
        return new string(code);
    }
}
