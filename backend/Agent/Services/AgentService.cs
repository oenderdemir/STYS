using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using STYS.Agent.Contracts.Dtos;
using STYS.Agent.Contracts.Enums;
using STYS.Agent.Entities;
using STYS.Infrastructure.EntityFramework;
using TOD.Platform.SharedKernel.Exceptions;
using AgentEntity = STYS.Agent.Entities.Agent;

namespace STYS.Agent.Services;

public sealed class AgentService : IAgentService
{
    private readonly IDbContextFactory<StysAppDbContext> _dbContextFactory;

    public AgentService(IDbContextFactory<StysAppDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<AgentDto> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var agent = await db.Set<AgentEntity>()
            .Include(x => x.Tesisler)
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
        if (agent is null)
            throw new BaseException("Agent bulunamadı.", 404);

        return MapToDto(agent);
    }

    public async Task<IReadOnlyCollection<AgentListDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Set<AgentEntity>()
            .Where(x => !x.IsDeleted)
            .Select(x => new AgentListDto
            {
                Id = x.Id,
                Ad = x.Ad,
                AgentKey = x.AgentKey,
                KurumId = x.KurumId,
                Durum = (int)x.Durum,
                AgentVersion = x.AgentVersion,
                SonGorulmeTarihi = x.SonGorulmeTarihi,
                CreatedAt = x.CreatedAt ?? DateTime.MinValue
            })
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<AgentDto> CreateAsync(AgentKaydetRequest request, string createdBy, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var agentKey = $"AGNT-{Guid.NewGuid():N}"[..16];
        var agent = new AgentEntity
        {
            Ad = request.Ad,
            AgentKey = agentKey,
            KurumId = request.KurumId,
            Durum = AgentDurum.Active,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow
        };

        db.Set<AgentEntity>().Add(agent);
        await db.SaveChangesAsync(cancellationToken);

        foreach (var tesisId in request.TesisIds)
        {
            db.Set<AgentTesis>().Add(new AgentTesis
            {
                AgentId = agent.Id,
                KurumId = request.KurumId,
                TesisId = tesisId,
                CreatedBy = createdBy,
                CreatedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        return MapToDto(agent);
    }

    public async Task<AgentDto> UpdateAsync(int id, AgentKaydetRequest request, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var agent = await db.Set<AgentEntity>()
            .Include(x => x.Tesisler)
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
        if (agent is null)
            throw new BaseException("Agent bulunamadı.", 404);

        agent.Ad = request.Ad;
        agent.KurumId = request.KurumId;

        var existingTesisIds = agent.Tesisler.Where(x => !x.IsDeleted).Select(x => x.TesisId).ToHashSet();
        foreach (var tesisId in request.TesisIds.Where(x => !existingTesisIds.Contains(x)))
        {
            db.Set<AgentTesis>().Add(new AgentTesis
            {
                AgentId = agent.Id,
                KurumId = request.KurumId,
                TesisId = tesisId,
                CreatedBy = agent.UpdatedBy,
                CreatedAt = DateTime.UtcNow
            });
        }

        foreach (var tesis in agent.Tesisler.Where(x => !request.TesisIds.Contains(x.TesisId)))
        {
            tesis.IsDeleted = true;
        }

        await db.SaveChangesAsync(cancellationToken);
        return MapToDto(agent);
    }

    public async Task ApproveAsync(int id, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var agent = await db.Set<AgentEntity>().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
        if (agent is null)
            throw new BaseException("Agent bulunamadı.", 404);
        if (agent.Durum != AgentDurum.PendingApproval)
            throw new BaseException("Sadece onay bekleyen agent onaylanabilir.", 400);

        agent.Durum = AgentDurum.Active;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DisableAsync(int id, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var agent = await db.Set<AgentEntity>().Include(x => x.Credentialler)
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
        if (agent is null)
            throw new BaseException("Agent bulunamadı.", 404);

        agent.Durum = AgentDurum.Disabled;
        foreach (var cred in agent.Credentialler.Where(x => x.AktifMi))
            cred.AktifMi = false;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeAsync(int id, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var agent = await db.Set<AgentEntity>().Include(x => x.Credentialler)
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
        if (agent is null)
            throw new BaseException("Agent bulunamadı.", 404);

        agent.Durum = AgentDurum.Revoked;
        foreach (var cred in agent.Credentialler)
        {
            cred.AktifMi = false;
            cred.RevokedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<AgentEnrollmentCodeDto> GenerateEnrollmentCodeAsync(AgentEnrollmentCodeRequest request, string createdBy, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var code = GenerateSecureCode();
        var enrollment = new AgentEnrollment
        {
            Code = code,
            KurumId = request.KurumId,
            TesisIds = System.Text.Json.JsonSerializer.Serialize(request.TesisIds),
            AllowedScopes = System.Text.Json.JsonSerializer.Serialize(request.AllowedScopes),
            MaxKullanimSayisi = request.MaxKullanimSayisi ?? 1,
            ExpiresAt = DateTime.UtcNow.AddHours(request.ExpirationHours ?? 24),
            Durum = AgentEnrollmentDurum.Active,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow
        };

        db.Set<AgentEnrollment>().Add(enrollment);
        await db.SaveChangesAsync(cancellationToken);

        return new AgentEnrollmentCodeDto
        {
            Id = enrollment.Id,
            Code = enrollment.Code,
            KurumId = enrollment.KurumId,
            TesisIds = request.TesisIds,
            AllowedScopes = request.AllowedScopes,
            KullanimSayisi = 0,
            MaxKullanimSayisi = enrollment.MaxKullanimSayisi,
            ExpiresAt = enrollment.ExpiresAt,
            Durum = (int)enrollment.Durum,
            CreatedAt = enrollment.CreatedAt ?? DateTime.UtcNow
        };
    }

    public async Task<IReadOnlyCollection<AgentEnrollmentCodeDto>> GetEnrollmentCodesAsync(CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var codes = await db.Set<AgentEnrollment>()
            .Where(x => !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return codes.Select(x =>
        {
            var tesisIds = System.Text.Json.JsonSerializer.Deserialize<List<int>>(x.TesisIds) ?? new List<int>();
            var allowedScopes = System.Text.Json.JsonSerializer.Deserialize<List<string>>(x.AllowedScopes) ?? new List<string>();
            return new AgentEnrollmentCodeDto
            {
                Id = x.Id,
                Code = x.Code,
                KurumId = x.KurumId,
                TesisIds = tesisIds,
                AllowedScopes = allowedScopes,
                KullanimSayisi = x.KullanimSayisi,
                MaxKullanimSayisi = x.MaxKullanimSayisi,
                ExpiresAt = x.ExpiresAt,
                Durum = (int)x.Durum,
                AgentId = x.AgentId,
                CreatedAt = x.CreatedAt ?? DateTime.MinValue
            };
        }).ToList();
    }

    public async Task RevokeEnrollmentCodeAsync(int enrollmentId, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var enrollment = await db.Set<AgentEnrollment>().FirstOrDefaultAsync(x => x.Id == enrollmentId && !x.IsDeleted, cancellationToken);
        if (enrollment is null)
            throw new BaseException("Enrollment kodu bulunamadı.", 404);

        enrollment.Durum = AgentEnrollmentDurum.Revoked;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static AgentDto MapToDto(AgentEntity agent)
    {
        var tesisIds = agent.Tesisler?.Where(x => !x.IsDeleted).Select(x => x.TesisId).ToList() ?? new List<int>();

        return new AgentDto
        {
            Id = agent.Id,
            Ad = agent.Ad,
            AgentKey = agent.AgentKey,
            KurumId = agent.KurumId,
            Durum = (int)agent.Durum,
            AgentVersion = agent.AgentVersion,
            SonGorulmeTarihi = agent.SonGorulmeTarihi,
            CihazKimligi = agent.CihazKimligi,
            TesisIds = tesisIds,
            CreatedAt = agent.CreatedAt ?? DateTime.MinValue
        };
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
