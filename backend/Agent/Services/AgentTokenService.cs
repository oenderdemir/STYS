using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using STYS.Agent.Contracts.Dtos;
using STYS.Agent.Contracts.Enums;
using STYS.Agent.Entities;
using STYS.Infrastructure.EntityFramework;
using TOD.Platform.Security.Auth.DTO;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;
using AgentEntity = STYS.Agent.Entities.Agent;

namespace STYS.Agent.Services;

public sealed class AgentTokenService : IAgentTokenService
{
    private readonly IDbContextFactory<StysAppDbContext> _dbContextFactory;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IOptions<AgentAuthOptions> _options;

    public AgentTokenService(
        IDbContextFactory<StysAppDbContext> dbContextFactory,
        IJwtTokenService jwtTokenService,
        IOptions<AgentAuthOptions> options)
    {
        _dbContextFactory = dbContextFactory;
        _jwtTokenService = jwtTokenService;
        _options = options;
    }

    public async Task<AgentEnrollmentResponse> EnrollAsync(AgentEnrollmentRequest request, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var enrollment = await db.Set<AgentEnrollment>()
            .FirstOrDefaultAsync(x => x.Code == request.EnrollmentCode && !x.IsDeleted, cancellationToken);
        if (enrollment is null)
            throw new BaseException("Geçersiz enrollment kodu.", 400);
        if (enrollment.Durum != AgentEnrollmentDurum.Active)
            throw new BaseException("Enrollment kodu artık geçerli değil.", 400);
        if (DateTime.UtcNow > enrollment.ExpiresAt)
        {
            enrollment.Durum = AgentEnrollmentDurum.Expired;
            await db.SaveChangesAsync(cancellationToken);
            throw new BaseException("Enrollment kodunun süresi dolmuş.", 400);
        }
        if (enrollment.KullanimSayisi >= enrollment.MaxKullanimSayisi)
            throw new BaseException("Enrollment kodu maksimum kullanım sayısına ulaştı.", 400);

        var allowedScopes = JsonSerializer.Deserialize<List<string>>(enrollment.AllowedScopes) ?? new List<string>();
        var allowedTesisIds = JsonSerializer.Deserialize<List<int>>(enrollment.TesisIds) ?? new List<int>();

        var agent = new AgentEntity
        {
            Ad = request.AgentKey,
            AgentKey = request.AgentKey,
            KurumId = enrollment.KurumId,
            Durum = AgentDurum.Active,
            AgentVersion = request.AgentVersion,
            CihazKimligi = request.CihazKimligi,
            PublicKey = request.PublicKey,
            CreatedBy = "agent-enrollment",
            CreatedAt = DateTime.UtcNow
        };

        db.Set<AgentEntity>().Add(agent);
        await db.SaveChangesAsync(cancellationToken);

        foreach (var tesisId in allowedTesisIds)
        {
            db.Set<AgentTesis>().Add(new AgentTesis
            {
                AgentId = agent.Id,
                KurumId = enrollment.KurumId,
                TesisId = tesisId,
                CreatedBy = "agent-enrollment",
                CreatedAt = DateTime.UtcNow
            });
        }

        var clientId = $"agent-{agent.Id}-{Guid.NewGuid():N}"[..24];
        var clientSecret = GenerateClientSecret();
        var clientSecretHash = ComputeSha256Hash(clientSecret);

        db.Set<AgentCredential>().Add(new AgentCredential
        {
            AgentId = agent.Id,
            KurumId = enrollment.KurumId,
            ClientId = clientId,
            ClientSecretHash = clientSecretHash,
            AktifMi = true,
            CreatedBy = "agent-enrollment",
            CreatedAt = DateTime.UtcNow
        });

        enrollment.KullanimSayisi++;
        enrollment.AgentId = agent.Id;
        if (enrollment.KullanimSayisi >= enrollment.MaxKullanimSayisi)
            enrollment.Durum = AgentEnrollmentDurum.Used;

        await db.SaveChangesAsync(cancellationToken);

        return new AgentEnrollmentResponse
        {
            AgentId = agent.Id,
            ClientId = clientId,
            ClientSecret = clientSecret,
            AgentKey = agent.AgentKey,
            Durum = (int)agent.Durum,
            Message = agent.Durum == AgentDurum.Active ? "Agent başarıyla kaydedildi." : "Agent kaydedildi, onay bekleniyor."
        };
    }

    public async Task<AgentTokenResponse> IssueTokenAsync(AgentTokenRequest request, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var credential = await db.Set<AgentCredential>()
            .Include(x => x.Agent)
            .FirstOrDefaultAsync(x => x.ClientId == request.ClientId && x.AktifMi && !x.IsDeleted, cancellationToken);
        if (credential is null)
            throw new BaseException("Geçersiz client kimliği.", 401);

        var expectedHash = ComputeSha256Hash(request.ClientSecret);
        if (!string.Equals(credential.ClientSecretHash, expectedHash, StringComparison.OrdinalIgnoreCase))
            throw new BaseException("Geçersiz client secret.", 401);

        if (credential.ExpiresAt.HasValue && DateTime.UtcNow > credential.ExpiresAt.Value)
            throw new BaseException("Credential süresi dolmuş.", 401);

        var agent = credential.Agent!;
        if (agent.Durum != AgentDurum.Active)
            throw new BaseException("Agent aktif değil.", 403);

        var tesisIds = await db.Set<AgentTesis>()
            .Where(x => x.AgentId == agent.Id && x.AktifMi && !x.IsDeleted)
            .Select(x => x.TesisId)
            .ToListAsync(cancellationToken);

        agent.SonGorulmeTarihi = DateTime.UtcNow;
        agent.AgentVersion = request.AgentVersion;
        await db.SaveChangesAsync(cancellationToken);

        var tokenRequest = new GenerateTokenRequest
        {
            UserName = $"agent:{agent.Id}",
            Name = agent.Ad,
            Email = $"{agent.AgentKey}@agent.stys.local",
            Surname = "",
            UserId = agent.Id.ToString(),
            KurumId = agent.KurumId,
            KurumIds = new List<int> { agent.KurumId },
            IsKurumAdmin = false,
            IsSuperAdmin = false,
            TokenVersion = 0
        };

        var tokenResponse = await _jwtTokenService.GenerateToken(tokenRequest, cancellationToken);

        return new AgentTokenResponse
        {
            AccessToken = tokenResponse.Token,
            ExpiresAt = tokenResponse.TokenExpireDate,
            TokenType = "Bearer"
        };
    }

    private static string GenerateClientSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }

    private static string ComputeSha256Hash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(bytes);
    }
}
