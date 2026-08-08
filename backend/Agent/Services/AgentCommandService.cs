using Microsoft.EntityFrameworkCore;
using STYS.Agent.Contracts.Dtos;
using STYS.Agent.Contracts.Enums;
using STYS.Agent.Entities;
using STYS.Infrastructure.EntityFramework;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;
using AgentEntity = STYS.Agent.Entities.Agent;

namespace STYS.Agent.Services;

public sealed class AgentCommandService
{
    private readonly IDbContextFactory<StysAppDbContext> _dbContextFactory;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public AgentCommandService(IDbContextFactory<StysAppDbContext> dbContextFactory, ICurrentTenantAccessor tenantAccessor)
    {
        _dbContextFactory = dbContextFactory;
        _tenantAccessor = tenantAccessor;
    }

    public async Task<AgentCommandDto> SendAsync(AgentCommandSendRequest request, string requestedBy, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var agent = await db.Set<AgentEntity>().FirstOrDefaultAsync(x => x.Id == request.AgentId && !x.IsDeleted, cancellationToken);
        if (agent is null) throw new BaseException("Agent bulunamadı.", 404);
        if (!_tenantAccessor.IsSuperAdmin() && !_tenantAccessor.GetAccessibleKurumIds().Contains(agent.KurumId))
            throw new BaseException("Bu agent'a komut gönderme yetkiniz yok.", 403);
        if (agent.Durum != AgentDurum.Active)
            throw new BaseException("Agent aktif değil.", 400);

        var scopes = await db.Set<AgentScope>().Where(x => x.AgentId == agent.Id && x.AktifMi && !x.IsDeleted).Select(x => x.Scope).ToListAsync(cancellationToken);
        var requiredScope = GetRequiredScope(request.CommandType);
        if (!string.IsNullOrWhiteSpace(requiredScope) && !scopes.Contains(requiredScope, StringComparer.OrdinalIgnoreCase))
            throw new BaseException("Agent gerekli scope'a sahip değil.", 403);

        var command = new AgentCommand
        {
            Id = Guid.NewGuid(),
            AgentId = agent.Id,
            KurumId = agent.KurumId,
            CommandType = request.CommandType,
            Payload = request.Payload,
            Status = AgentCommandStatus.Pending,
            Priority = request.Priority,
            ExpiresAt = request.ExpirationMinutes.HasValue ? DateTime.UtcNow.AddMinutes(request.ExpirationMinutes.Value) : null,
            MaxRetryCount = request.MaxRetryCount,
            CorrelationId = Guid.NewGuid().ToString("N"),
            IdempotencyKey = $"{request.CommandType}:{Guid.NewGuid():N}"[..64],
            RequestedBy = requestedBy,
            CreatedBy = requestedBy,
            CreatedAt = DateTime.UtcNow
        };

        db.Set<AgentCommand>().Add(command);
        await db.SaveChangesAsync(cancellationToken);

        return MapToDto(command);
    }

    public async Task<IReadOnlyCollection<AgentCommandDto>> GetPendingCommandsAsync(int agentId, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTime.UtcNow;

        var commands = await db.Set<AgentCommand>()
            .Where(x => x.AgentId == agentId && !x.IsDeleted && x.Status == AgentCommandStatus.Pending)
            .Where(x => x.ExpiresAt == null || x.ExpiresAt > now)
            .OrderByDescending(x => x.Priority)
            .ThenBy(x => x.CreatedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        return commands.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyCollection<AgentCommandDto>> GetHistoryAsync(int agentId, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Set<AgentCommand>()
            .Where(x => x.AgentId == agentId && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .Take(100)
            .Select(x => new AgentCommandDto
            {
                Id = x.Id, AgentId = x.AgentId, CommandType = x.CommandType, Payload = x.Payload,
                Status = (int)x.Status, Priority = x.Priority, ScheduledAt = x.ScheduledAt,
                ExpiresAt = x.ExpiresAt, RetryCount = x.RetryCount, MaxRetryCount = x.MaxRetryCount,
                CorrelationId = x.CorrelationId, IdempotencyKey = x.IdempotencyKey, CreatedAt = x.CreatedAt ?? DateTime.MinValue
            })
            .ToListAsync(cancellationToken);
    }

    public async Task AcceptAsync(Guid commandId, int agentId, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var command = await db.Set<AgentCommand>().FirstOrDefaultAsync(x => x.Id == commandId && x.AgentId == agentId && !x.IsDeleted, cancellationToken);
        if (command is null) throw new BaseException("Komut bulunamadı.", 404);

        if (command.Status != AgentCommandStatus.Pending && command.Status != AgentCommandStatus.Delivered)
            throw new BaseException("Komut bu durumda kabul edilemez.", 400);

        if (command.ExpiresAt.HasValue && DateTime.UtcNow > command.ExpiresAt.Value)
        {
            command.Status = AgentCommandStatus.Expired;
            await db.SaveChangesAsync(cancellationToken);
            throw new BaseException("Komut süresi dolmuş.", 400);
        }

        var prevStatus = command.Status;
        command.Status = AgentCommandStatus.Accepted;
        command.StartedAt = DateTime.UtcNow;

        db.Set<AgentCommandExecution>().Add(new AgentCommandExecution
        {
            CommandId = command.Id, AgentId = agentId, KurumId = command.KurumId,
            Status = "Accepted", PreviousStatus = prevStatus.ToString(), MachineName = Environment.MachineName,
            CreatedBy = "agent", CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task CompleteAsync(Guid commandId, int agentId, AgentCommandCompleteRequest request, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var command = await db.Set<AgentCommand>().FirstOrDefaultAsync(x => x.Id == commandId && x.AgentId == agentId && !x.IsDeleted, cancellationToken);
        if (command is null) throw new BaseException("Komut bulunamadı.", 404);

        if (command.Status != AgentCommandStatus.Accepted && command.Status != AgentCommandStatus.Running)
            throw new BaseException("Komut bu durumda tamamlanamaz.", 400);

        var prevStatus = command.Status;
        command.Status = request.Success ? AgentCommandStatus.Completed : AgentCommandStatus.Failed;
        command.CompletedAt = DateTime.UtcNow;
        command.ResultPayload = request.ResultPayload;
        command.ErrorCode = request.ErrorCode;
        command.ErrorMessage = request.ErrorMessage;

        db.Set<AgentCommandExecution>().Add(new AgentCommandExecution
        {
            CommandId = command.Id, AgentId = agentId, KurumId = command.KurumId,
            Status = request.Success ? "Completed" : "Failed", PreviousStatus = prevStatus.ToString(),
            ErrorCode = request.ErrorCode, ErrorMessage = request.ErrorMessage,
            MachineName = Environment.MachineName, CreatedBy = "agent", CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task FailAsync(Guid commandId, int agentId, string errorMessage, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var command = await db.Set<AgentCommand>().FirstOrDefaultAsync(x => x.Id == commandId && x.AgentId == agentId && !x.IsDeleted, cancellationToken);
        if (command is null) throw new BaseException("Komut bulunamadı.", 404);

        var prevStatus = command.Status;
        command.Status = AgentCommandStatus.Failed;
        command.CompletedAt = DateTime.UtcNow;
        command.ErrorMessage = errorMessage;

        db.Set<AgentCommandExecution>().Add(new AgentCommandExecution
        {
            CommandId = command.Id, AgentId = agentId, KurumId = command.KurumId,
            Status = "Failed", PreviousStatus = prevStatus.ToString(), ErrorMessage = errorMessage,
            MachineName = Environment.MachineName, CreatedBy = "agent", CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    private static AgentCommandDto MapToDto(AgentCommand c) => new()
    {
        Id = c.Id, AgentId = c.AgentId, CommandType = c.CommandType, Payload = c.Payload,
        Status = (int)c.Status, Priority = c.Priority, ScheduledAt = c.ScheduledAt,
        ExpiresAt = c.ExpiresAt, RetryCount = c.RetryCount, MaxRetryCount = c.MaxRetryCount,
        CorrelationId = c.CorrelationId, IdempotencyKey = c.IdempotencyKey, CreatedAt = c.CreatedAt ?? DateTime.MinValue
    };

    private static string? GetRequiredScope(string commandType) => commandType switch
    {
        "Ping" => "agent.command.execute",
        "HealthCheck" => "agent.command.execute",
        "RefreshConfiguration" => "agent.config.read",
        "PavoConnectionTest" => "stys.pavo.connection.test",
        _ => "agent.command.execute"
    };
}
