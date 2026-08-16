using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using STYS.Agent.Contracts.Dtos;
using STYS.Agent.Contracts.Enums;
using STYS.Agent.Entities;
using STYS.Infrastructure.EntityFramework;
using STYS.Kurumlar.Entities;
using STYS.Tesisler.Entities;
using TOD.Platform.SharedKernel.Exceptions;
using AgentEntity = STYS.Agent.Entities.Agent;

namespace STYS.Agent.Services;

public sealed class AgentTokenService : IAgentTokenService
{
    /// <summary>Single generic rejection reason for the anonymous enrollment endpoint, so probing
    /// cannot distinguish unknown / expired / already-used / revoked codes.</summary>
    private const string InvalidEnrollmentMessage = "Enrollment kodu geçersiz veya kullanılamaz durumda.";

    private readonly IDbContextFactory<StysAppDbContext> _dbContextFactory;
    private readonly IAgentJwtTokenService _jwtTokenService;
    private readonly IAgentEnrollmentExecutionHook? _hook;
    private readonly IAgentRealtimeNotifier? _realtimeNotifier;

    public AgentTokenService(
        IDbContextFactory<StysAppDbContext> dbContextFactory,
        IAgentJwtTokenService jwtTokenService,
        IAgentEnrollmentExecutionHook? hook = null,
        IAgentRealtimeNotifier? realtimeNotifier = null)
    {
        _dbContextFactory = dbContextFactory;
        _jwtTokenService = jwtTokenService;
        _hook = hook;
        _realtimeNotifier = realtimeNotifier;
    }

    public async Task<AgentEnrollmentResponse> EnrollAsync(AgentEnrollmentRequest request, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        db.AllowExplicitTenantWritesWithoutAmbientTenant = true;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var transactionCompleted = false;

        try
        {
            // Lookup is by hash; the plaintext code is never stored, so it cannot be read back out
            // of the database even by an operator with full table access.
            var codeHash = AgentEnrollmentCodeHasher.Hash(request.EnrollmentCode ?? string.Empty);
            var enrollment = await db.Set<AgentEnrollment>()
                .IgnoreQueryFilters()
                .Include(x => x.InstallationSession)
                .FirstOrDefaultAsync(x => x.CodeHash == codeHash && !x.IsDeleted, cancellationToken);

            if (enrollment is null)
                throw new BaseException(InvalidEnrollmentMessage, 400);

            // The code is spent. Before rejecting, check whether this is the very installation that
            // consumed it retrying because the original response never arrived. Proving possession
            // of the registration nonce AND matching the recorded installation identity is the only
            // way through; for everyone else a used code stays a flat rejection.
            if (IsRecoveryAttempt(enrollment, request))
            {
                var recovered = await RecoverRegistrationAsync(db, enrollment, request, cancellationToken);
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                transactionCompleted = true;
                return recovered;
            }

            // This endpoint is anonymous, so every rejection below returns the same generic message.
            // Distinguishing "unknown" from "expired" from "already used" would tell an attacker
            // probing codes which guesses were real.
            if (enrollment.Durum != AgentEnrollmentDurum.Active
                || DateTime.UtcNow > enrollment.ExpiresAt
                || enrollment.KullanimSayisi >= enrollment.MaxKullanimSayisi)
            {
                throw new BaseException(InvalidEnrollmentMessage, 400);
            }

            if (enrollment.InstallationSession is not null)
            {
                var session = enrollment.InstallationSession;

                // A kurum mismatch is a server-side integrity problem, not something a caller should
                // be able to distinguish by probing, so it collapses into the same generic answer.
                if (session.KurumId != enrollment.KurumId)
                    throw new BaseException(InvalidEnrollmentMessage, 400);

                if (DateTime.UtcNow > session.ExpiresAt)
                {
                    await transaction.CommitAsync(cancellationToken);
                    transactionCompleted = true;
                    await PersistExpiredInstallationSessionAsync(session.Id, enrollment.Id, cancellationToken);
                    throw new BaseException(InvalidEnrollmentMessage, 400);
                }

                if (session.Status is AgentInstallationSessionStatus.Cancelled
                    or AgentInstallationSessionStatus.Expired
                    or AgentInstallationSessionStatus.Failed
                    or AgentInstallationSessionStatus.Completed
                    or AgentInstallationSessionStatus.PendingApproval
                    or AgentInstallationSessionStatus.Enrolled
                    or AgentInstallationSessionStatus.Online)
                {
                    throw new BaseException(InvalidEnrollmentMessage, 400);
                }
            }

            var allowedScopes = JsonSerializer.Deserialize<List<string>>(enrollment.AllowedScopes) ?? new List<string>();
            var allowedTesisIds = JsonSerializer.Deserialize<List<int>>(enrollment.TesisIds) ?? new List<int>();

            await ValidateTesisIdsAsync(db, enrollment.KurumId, allowedTesisIds, cancellationToken);

            // Kurum policy is the source of truth and can only ever ADD approval, never remove it.
            // An enrollment code created with RequiresApproval=false cannot bypass a kurum that
            // mandates approval; conversely an operator may still demand approval for one specific
            // installation even when the kurum does not require it.
            var kurumRequiresApproval = await db.Set<Kurum>()
                .IgnoreQueryFilters()
                .Where(x => x.Id == enrollment.KurumId && !x.IsDeleted)
                .Select(x => (bool?)x.AgentEnrollmentRequiresApproval)
                .FirstOrDefaultAsync(cancellationToken);

            // A missing kurum row is treated as "approval required" rather than silently activating.
            var requiresApproval = (kurumRequiresApproval ?? true) || enrollment.RequiresApproval;
            var agentDurum = requiresApproval ? AgentDurum.PendingApproval : AgentDurum.Active;
            var agent = await db.Set<AgentEntity>()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.KurumId == enrollment.KurumId && x.AgentKey == request.AgentKey && !x.IsDeleted, cancellationToken);
            var agentDisplayName = enrollment.InstallationSession?.AgentDisplayName ?? NormalizeAgentDisplayName(request.AgentDisplayName, request.AgentKey);
            var runtimeIdentifier = enrollment.InstallationSession?.TargetRid;

            if (agent is null)
            {
                agent = new AgentEntity
                {
                    Ad = agentDisplayName,
                    AgentKey = request.AgentKey,
                    KurumId = enrollment.KurumId,
                    Durum = agentDurum,
                    AgentVersion = request.AgentVersion,
                    RuntimeIdentifier = runtimeIdentifier,
                    CihazKimligi = request.CihazKimligi,
                    PublicKey = request.PublicKey,
                    CreatedBy = "agent-enrollment",
                    CreatedAt = DateTime.UtcNow
                };
                db.Set<AgentEntity>().Add(agent);
                await db.SaveChangesAsync(cancellationToken);
            }
            else
            {
                await RemoveExistingAgentEnrollmentDataAsync(db, agent.Id, cancellationToken);
                agent.Ad = agentDisplayName;
                agent.Durum = agentDurum;
                agent.AgentVersion = request.AgentVersion;
                agent.RuntimeIdentifier = runtimeIdentifier;
                agent.CihazKimligi = request.CihazKimligi;
                agent.PublicKey = request.PublicKey;
                agent.IsDeleted = false;
                await db.SaveChangesAsync(cancellationToken);
            }

            foreach (var tesisId in allowedTesisIds)
                db.Set<AgentTesis>().Add(new AgentTesis { AgentId = agent.Id, KurumId = enrollment.KurumId, TesisId = tesisId, CreatedBy = "agent-enrollment", CreatedAt = DateTime.UtcNow });

            foreach (var scope in allowedScopes)
            {
                var normalized = scope.ToLowerInvariant().Trim();
                db.Set<AgentScope>().Add(new AgentScope { AgentId = agent.Id, KurumId = enrollment.KurumId, Scope = normalized, AktifMi = true, CreatedBy = "agent-enrollment", CreatedAt = DateTime.UtcNow });
            }

            foreach (var capability in request.Capabilities
                         .Select(x => x.Trim().ToLowerInvariant())
                         .Where(x => !string.IsNullOrWhiteSpace(x))
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                db.Set<AgentCapability>().Add(new AgentCapability
                {
                    AgentId = agent.Id,
                    KurumId = enrollment.KurumId,
                    Capability = capability,
                    AktifMi = true,
                    CreatedBy = "agent-enrollment",
                    CreatedAt = DateTime.UtcNow
                });
            }

            var clientId = $"agent-{agent.Id}-{Guid.NewGuid():N}"[..24];
            var clientSecret = GenerateClientSecret();
            var clientSecretHash = ComputeSha256Hash(clientSecret);
            db.Set<AgentCredential>().Add(new AgentCredential { AgentId = agent.Id, KurumId = enrollment.KurumId, ClientId = clientId, ClientSecretHash = clientSecretHash, AktifMi = true, CredentialVersion = 1, CreatedBy = "agent-enrollment", CreatedAt = DateTime.UtcNow });

            if (_hook is not null)
                await _hook.AfterEntitiesCreatedBeforeCommitAsync(agent, cancellationToken);

            enrollment.KullanimSayisi++;
            enrollment.AgentId = agent.Id;
            // Only the hash is kept, so the recovery proof cannot be lifted from the database.
            enrollment.RegistrationNonceHash = string.IsNullOrWhiteSpace(request.RegistrationNonce)
                ? null
                : ComputeSha256Hash(request.RegistrationNonce);
            // Rotating the concurrency token makes the enrollment row the serialization point: two
            // parallel registrations with the same code both read the same original token, so only
            // the first SaveChanges matches and the loser fails the concurrency check below.
            enrollment.ConcurrencyToken = Guid.NewGuid();
            if (enrollment.KullanimSayisi >= enrollment.MaxKullanimSayisi)
                enrollment.Durum = AgentEnrollmentDurum.Used;

            if (enrollment.InstallationSession is not null)
            {
                enrollment.InstallationSession.EnrolledAgentId = agent.Id;
                // Must mirror the effective decision (kurum policy included), not just the code's flag.
                enrollment.InstallationSession.Status = requiresApproval
                    ? AgentInstallationSessionStatus.PendingApproval
                    : AgentInstallationSessionStatus.Enrolled;
                enrollment.InstallationSession.UpdatedAt = DateTime.UtcNow;
            }

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            transactionCompleted = true;

            if (_realtimeNotifier is not null)
                await _realtimeNotifier.AgentChangedAsync(cancellationToken);

            return new AgentEnrollmentResponse { AgentId = agent.Id, ClientId = clientId, ClientSecret = clientSecret, AgentKey = agent.AgentKey, Durum = (int)agent.Durum, Message = agent.Durum == AgentDurum.Active ? "Agent başarıyla kaydedildi." : "Agent kaydedildi, onay bekleniyor." };
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another registration consumed this enrollment code first. Reject with the same
            // generic message so a losing race is indistinguishable from any other invalid code.
            if (!transactionCompleted)
                await transaction.RollbackAsync(cancellationToken);
            throw new BaseException(InvalidEnrollmentMessage, 400);
        }
        catch
        {
            if (!transactionCompleted)
                await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>True when a consumed enrollment is being retried by the exact installation that
    /// consumed it. Requires all three of: a stored nonce hash, a matching presented nonce, and the
    /// same installation identity as the agent that was created. Anything less is not a recovery.</summary>
    private static bool IsRecoveryAttempt(AgentEnrollment enrollment, AgentEnrollmentRequest request)
    {
        if (enrollment.AgentId is null || string.IsNullOrWhiteSpace(enrollment.RegistrationNonceHash))
            return false;

        if (string.IsNullOrWhiteSpace(request.RegistrationNonce))
            return false;

        var presented = ComputeSha256Hash(request.RegistrationNonce);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(enrollment.RegistrationNonceHash),
            Encoding.UTF8.GetBytes(presented));
    }

    /// <summary>Completes a registration whose response was lost. Reuses the agent that already
    /// exists (never creates a second one), revokes the orphaned credential the agent never
    /// received, and issues a fresh one. The new plaintext secret exists only in this response.</summary>
    private static async Task<AgentEnrollmentResponse> RecoverRegistrationAsync(
        StysAppDbContext db,
        AgentEnrollment enrollment,
        AgentEnrollmentRequest request,
        CancellationToken cancellationToken)
    {
        var agent = await db.Set<AgentEntity>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == enrollment.AgentId!.Value && !x.IsDeleted, cancellationToken)
            ?? throw new BaseException(InvalidEnrollmentMessage, 400);

        // Second binding: the nonce alone is not enough, the retry must come from the same machine
        // identity that the original registration recorded.
        if (!string.IsNullOrWhiteSpace(agent.CihazKimligi)
            && !string.Equals(agent.CihazKimligi, request.CihazKimligi, StringComparison.OrdinalIgnoreCase))
        {
            throw new BaseException(InvalidEnrollmentMessage, 400);
        }

        // The previously issued credential either never arrived or is unusable; retire it so a
        // recovered registration never leaves two live credentials behind.
        var orphaned = await db.Set<AgentCredential>()
            .IgnoreQueryFilters()
            .Where(x => x.AgentId == agent.Id && x.AktifMi && !x.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var credential in orphaned)
        {
            credential.AktifMi = false;
            credential.CredentialVersion++;
            credential.RevokedAt = DateTime.UtcNow;
        }

        var clientId = $"agent-{agent.Id}-{Guid.NewGuid():N}"[..24];
        var clientSecret = GenerateClientSecret();
        db.Set<AgentCredential>().Add(new AgentCredential
        {
            AgentId = agent.Id,
            KurumId = enrollment.KurumId,
            ClientId = clientId,
            ClientSecretHash = ComputeSha256Hash(clientSecret),
            AktifMi = true,
            CredentialVersion = 1,
            CreatedBy = "agent-enrollment-recovery",
            CreatedAt = DateTime.UtcNow
        });

        return new AgentEnrollmentResponse
        {
            AgentId = agent.Id,
            ClientId = clientId,
            ClientSecret = clientSecret,
            AgentKey = agent.AgentKey,
            Durum = (int)agent.Durum,
            Message = agent.Durum == AgentDurum.Active
                ? "Agent kaydı kurtarıldı."
                : "Agent kaydı kurtarıldı, onay bekleniyor."
        };
    }

    private async Task PersistExpiredInstallationSessionAsync(int sessionId, int enrollmentId, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        db.AllowExplicitTenantWritesWithoutAmbientTenant = true;

        var session = await db.Set<AgentInstallationSession>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == sessionId && !x.IsDeleted, cancellationToken);
        if (session is not null)
        {
            session.Status = AgentInstallationSessionStatus.Expired;
            session.UpdatedAt = DateTime.UtcNow;
        }

        var enrollment = await db.Set<AgentEnrollment>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == enrollmentId && !x.IsDeleted, cancellationToken);
        if (enrollment is not null)
        {
            enrollment.Durum = AgentEnrollmentDurum.Expired;
            enrollment.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task RemoveExistingAgentEnrollmentDataAsync(StysAppDbContext db, int agentId, CancellationToken ct)
    {
        var existingCredentials = await db.Set<AgentCredential>()
            .IgnoreQueryFilters()
            .Where(x => x.AgentId == agentId && !x.IsDeleted)
            .ToListAsync(ct);
        var existingScopes = await db.Set<AgentScope>()
            .IgnoreQueryFilters()
            .Where(x => x.AgentId == agentId && !x.IsDeleted)
            .ToListAsync(ct);
        var existingTesis = await db.Set<AgentTesis>()
            .IgnoreQueryFilters()
            .Where(x => x.AgentId == agentId && !x.IsDeleted)
            .ToListAsync(ct);
        var existingCapabilities = await db.Set<AgentCapability>()
            .IgnoreQueryFilters()
            .Where(x => x.AgentId == agentId && !x.IsDeleted)
            .ToListAsync(ct);

        if (existingCredentials.Count > 0)
            db.RemoveRange(existingCredentials);
        if (existingScopes.Count > 0)
            db.RemoveRange(existingScopes);
        if (existingTesis.Count > 0)
            db.RemoveRange(existingTesis);
        if (existingCapabilities.Count > 0)
            db.RemoveRange(existingCapabilities);

        if (existingCredentials.Count > 0 || existingScopes.Count > 0 || existingTesis.Count > 0 || existingCapabilities.Count > 0)
            await db.SaveChangesAsync(ct);
    }

    /// <summary>Lets a registered-but-not-yet-approved agent discover its lifecycle status without
    /// granting it any operational API access. Authentication is by credential because a
    /// PendingApproval agent cannot obtain an access token.</summary>
    public async Task<AgentEnrollmentStatusResponse> GetEnrollmentStatusAsync(AgentEnrollmentStatusRequest request, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var credential = await db.Set<AgentCredential>()
            .IgnoreQueryFilters()
            .Include(x => x.Agent)
            .FirstOrDefaultAsync(x => x.ClientId == request.ClientId && !x.IsDeleted, cancellationToken);

        // Credential validation mirrors IssueTokenAsync, except a revoked/inactive credential is
        // still allowed to read back a terminal status so the agent can stop retrying and report
        // why. It never yields a token or any other agent data.
        if (credential is null)
            throw new BaseException("Geçersiz client kimliği.", 401);

        var expectedHash = ComputeSha256Hash(request.ClientSecret ?? string.Empty);
        if (!string.Equals(credential.ClientSecretHash, expectedHash, StringComparison.OrdinalIgnoreCase))
            throw new BaseException("Geçersiz client secret.", 401);

        var agent = credential.Agent ?? throw new BaseException("Agent bulunamadı.", 404);

        return new AgentEnrollmentStatusResponse
        {
            AgentId = agent.Id,
            Durum = (int)agent.Durum,
            Approved = agent.Durum == AgentDurum.Active,
            PendingApproval = agent.Durum == AgentDurum.PendingApproval,
            Message = agent.Durum switch
            {
                AgentDurum.Active => "Agent onaylandı.",
                AgentDurum.PendingApproval => "Agent onay bekliyor.",
                AgentDurum.Rejected => "Agent kaydı reddedildi.",
                AgentDurum.Disabled => "Agent devre dışı bırakıldı.",
                AgentDurum.Revoked => "Agent iptal edildi.",
                _ => null
            }
        };
    }

    public async Task<AgentTokenResponse> IssueTokenAsync(AgentTokenRequest request, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var credential = await db.Set<AgentCredential>()
            .IgnoreQueryFilters()
            .Include(x => x.Agent)
            .FirstOrDefaultAsync(x => x.ClientId == request.ClientId && !x.IsDeleted, cancellationToken);
        if (credential is null) throw new BaseException("Geçersiz client kimliği.", 401);

        var expectedHash = ComputeSha256Hash(request.ClientSecret);
        if (!string.Equals(credential.ClientSecretHash, expectedHash, StringComparison.OrdinalIgnoreCase))
            throw new BaseException("Geçersiz client secret.", 401);
        if (!credential.AktifMi || credential.RevokedAt.HasValue)
            throw new BaseException("Credential iptal edilmiş.", 401);
        if (credential.ExpiresAt.HasValue && DateTime.UtcNow > credential.ExpiresAt.Value)
            throw new BaseException("Credential süresi dolmuş.", 401);

        var agent = credential.Agent!;
        if (agent.Durum == AgentDurum.Disabled) throw new BaseException("Agent devre dışı bırakılmış.", 403);
        if (agent.Durum == AgentDurum.Revoked) throw new BaseException("Agent iptal edilmiş.", 403);
        if (agent.Durum == AgentDurum.Rejected) throw new BaseException("Agent kaydı reddedilmiş.", 403);
        if (agent.Durum == AgentDurum.PendingApproval) throw new BaseException("Agent henüz onaylanmamış.", 403);

        var tesisIds = await db.Set<AgentTesis>()
            .IgnoreQueryFilters()
            .Where(x => x.AgentId == agent.Id && x.AktifMi && !x.IsDeleted)
            .Select(x => x.TesisId).ToListAsync(cancellationToken);

        var scopes = await db.Set<AgentScope>()
            .IgnoreQueryFilters()
            .Where(x => x.AgentId == agent.Id && x.AktifMi && !x.IsDeleted)
            .Select(x => x.Scope).ToListAsync(cancellationToken);

        var descriptor = new AgentTokenDescriptor
        {
            AgentId = agent.Id, AgentKey = agent.AgentKey, AgentVersion = request.AgentVersion,
            KurumId = agent.KurumId, TesisIds = tesisIds, Scopes = scopes,
            AgentInstanceId = request.AgentInstanceId,
            CredentialId = credential.Id, CredentialVersion = credential.CredentialVersion
        };

        return await _jwtTokenService.GenerateTokenAsync(descriptor, cancellationToken);
    }

    private static async Task ValidateTesisIdsAsync(StysAppDbContext db, int kurumId, List<int> tesisIds, CancellationToken ct)
    {
        if (tesisIds.Count == 0) return;
        var valid = await db.Set<Tesis>()
            .IgnoreQueryFilters()
            .Where(x => tesisIds.Contains(x.Id) && x.KurumId == kurumId && !x.IsDeleted)
            .Select(x => x.Id)
            .ToListAsync(ct);
        var invalid = tesisIds.Where(x => !valid.Contains(x)).ToList();
        if (invalid.Count > 0) throw new BaseException($"Geçersiz tesis ID'leri: {string.Join(", ", invalid)}", 400);
    }

    private static string GenerateClientSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }

    private static string ComputeSha256Hash(string input) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(input)));

    private static string NormalizeAgentDisplayName(string? displayName, string agentKey) =>
        string.IsNullOrWhiteSpace(displayName) ? agentKey : displayName.Trim();
}
