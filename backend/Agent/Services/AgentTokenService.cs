using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using STYS.Agent.Contracts.Dtos;
using STYS.Agent.Contracts.Enums;
using STYS.Agent.Entities;
using STYS.Infrastructure.EntityFramework;
using STYS.Tesisler.Entities;
using TOD.Platform.SharedKernel.Exceptions;
using AgentEntity = STYS.Agent.Entities.Agent;

namespace STYS.Agent.Services;

public sealed class AgentTokenService : IAgentTokenService
{
    private readonly IDbContextFactory<StysAppDbContext> _dbContextFactory;
    private readonly IAgentJwtTokenService _jwtTokenService;

    public AgentTokenService(IDbContextFactory<StysAppDbContext> dbContextFactory, IAgentJwtTokenService jwtTokenService)
    {
        _dbContextFactory = dbContextFactory;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<AgentEnrollmentResponse> EnrollAsync(AgentEnrollmentRequest request, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var enrollment = await db.Set<AgentEnrollment>()
                .FirstOrDefaultAsync(x => x.Code == request.EnrollmentCode && !x.IsDeleted, cancellationToken);

            if (enrollment is null)
                throw new BaseException("Geçersiz enrollment kodu.", 400);
            if (enrollment.Durum != AgentEnrollmentDurum.Active)
                throw new BaseException("Enrollment kodu artık geçerli değil.", 400);
            if (DateTime.UtcNow > enrollment.ExpiresAt)
                throw new BaseException("Enrollment kodunun süresi dolmuş.", 400);
            if (enrollment.KullanimSayisi >= enrollment.MaxKullanimSayisi)
                throw new BaseException("Enrollment kodu maksimum kullanım sayısına ulaştı.", 400);

            var allowedScopes = JsonSerializer.Deserialize<List<string>>(enrollment.AllowedScopes) ?? new List<string>();
            var allowedTesisIds = JsonSerializer.Deserialize<List<int>>(enrollment.TesisIds) ?? new List<int>();

            await ValidateTesisIdsAsync(db, enrollment.KurumId, allowedTesisIds, cancellationToken);

            var agentDurum = enrollment.RequiresApproval ? AgentDurum.PendingApproval : AgentDurum.Active;
            var agent = new AgentEntity
            {
                Ad = request.AgentKey, AgentKey = request.AgentKey, KurumId = enrollment.KurumId, Durum = agentDurum,
                AgentVersion = request.AgentVersion, CihazKimligi = request.CihazKimligi, PublicKey = request.PublicKey,
                CreatedBy = "agent-enrollment", CreatedAt = DateTime.UtcNow
            };
            db.Set<AgentEntity>().Add(agent);
            await db.SaveChangesAsync(cancellationToken);

            foreach (var tesisId in allowedTesisIds)
                db.Set<AgentTesis>().Add(new AgentTesis { AgentId = agent.Id, KurumId = enrollment.KurumId, TesisId = tesisId, CreatedBy = "agent-enrollment", CreatedAt = DateTime.UtcNow });

            foreach (var scope in allowedScopes)
            {
                var normalized = scope.ToLowerInvariant().Trim();
                db.Set<AgentScope>().Add(new AgentScope { AgentId = agent.Id, KurumId = enrollment.KurumId, Scope = normalized, AktifMi = true, CreatedBy = "agent-enrollment", CreatedAt = DateTime.UtcNow });
            }

            var clientId = $"agent-{agent.Id}-{Guid.NewGuid():N}"[..24];
            var clientSecret = GenerateClientSecret();
            var clientSecretHash = ComputeSha256Hash(clientSecret);
            db.Set<AgentCredential>().Add(new AgentCredential { AgentId = agent.Id, KurumId = enrollment.KurumId, ClientId = clientId, ClientSecretHash = clientSecretHash, AktifMi = true, CredentialVersion = 1, CreatedBy = "agent-enrollment", CreatedAt = DateTime.UtcNow });

            enrollment.KullanimSayisi++;
            enrollment.AgentId = agent.Id;
            enrollment.ConcurrencyToken = Guid.NewGuid();
            if (enrollment.KullanimSayisi >= enrollment.MaxKullanimSayisi)
                enrollment.Durum = AgentEnrollmentDurum.Used;

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new AgentEnrollmentResponse { AgentId = agent.Id, ClientId = clientId, ClientSecret = clientSecret, AgentKey = agent.AgentKey, Durum = (int)agent.Durum, Message = agent.Durum == AgentDurum.Active ? "Agent başarıyla kaydedildi." : "Agent kaydedildi, onay bekleniyor." };
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<AgentTokenResponse> IssueTokenAsync(AgentTokenRequest request, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var credential = await db.Set<AgentCredential>()
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
        if (agent.Durum == AgentDurum.PendingApproval) throw new BaseException("Agent henüz onaylanmamış.", 403);

        var tesisIds = await db.Set<AgentTesis>()
            .Where(x => x.AgentId == agent.Id && x.AktifMi && !x.IsDeleted)
            .Select(x => x.TesisId).ToListAsync(cancellationToken);

        var scopes = await db.Set<AgentScope>()
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
        var valid = await db.Set<Tesis>().Where(x => tesisIds.Contains(x.Id) && x.KurumId == kurumId && !x.IsDeleted).Select(x => x.Id).ToListAsync(ct);
        var invalid = tesisIds.Where(x => !valid.Contains(x)).ToList();
        if (invalid.Count > 0) throw new BaseException($"Geçersiz tesis ID'leri: {string.Join(", ", invalid)}", 400);
    }

    private static string GenerateClientSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }

    private static string ComputeSha256Hash(string input) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
}
