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

    private static readonly Dictionary<string, string> CommandScopeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Ping"] = "agent.command.execute",
        ["HealthCheck"] = "agent.command.execute",
        ["RefreshConfiguration"] = "agent.config.read",
        ["PavoConnectionTest"] = "stys.pavo.connection.test"
    };

    private static readonly Dictionary<string, string> CommandCapabilityMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["PavoConnectionTest"] = "pavo"
    };

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

        await ValidateScopeAsync(db, agent.Id, request.CommandType, cancellationToken);
        await ValidateCapabilityAsync(db, agent.Id, request.CommandType, cancellationToken);

        var idempotencyKey = $"{request.CommandType}:{Guid.NewGuid():N}"[..64];

        var command = new AgentCommand
        {
            Id = Guid.NewGuid(), AgentId = agent.Id, KurumId = agent.KurumId,
            CommandType = request.CommandType, Payload = request.Payload,
            Status = AgentCommandStatus.Pending, Priority = request.Priority,
            ExpiresAt = request.ExpirationMinutes.HasValue ? DateTime.UtcNow.AddMinutes(request.ExpirationMinutes.Value) : null,
            MaxRetryCount = request.MaxRetryCount,
            CorrelationId = Guid.NewGuid().ToString("N"), IdempotencyKey = idempotencyKey,
            RequestedBy = requestedBy, CreatedBy = requestedBy, CreatedAt = DateTime.UtcNow
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
            .OrderByDescending(x => x.Priority).ThenBy(x => x.CreatedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        foreach (var cmd in commands)
        {
            cmd.Status = AgentCommandStatus.Delivered;
        }

        await db.SaveChangesAsync(cancellationToken);
        return commands.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyCollection<AgentCommandDto>> GetHistoryAsync(int agentId, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Set<AgentCommand>()
            .Where(x => x.AgentId == agentId && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt).Take(100)
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

        AgentCommandStateMachine.EnforceTransition(command.Status, AgentCommandStatus.Accepted, command.Id);

        if (command.ExpiresAt.HasValue && DateTime.UtcNow > command.ExpiresAt.Value)
        {
            command.Status = AgentCommandStatus.Expired;
            await db.SaveChangesAsync(cancellationToken);
            throw new BaseException("Komut süresi dolmuş.", 400);
        }

        command.Status = AgentCommandStatus.Accepted;
        command.StartedAt = DateTime.UtcNow;

        db.Set<AgentCommandExecution>().Add(new AgentCommandExecution
        {
            CommandId = command.Id, AgentId = agentId, KurumId = command.KurumId,
            Status = "Accepted", PreviousStatus = AgentCommandStatus.Delivered.ToString(),
            MachineName = Environment.MachineName, CreatedBy = "agent", CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task CompleteAsync(Guid commandId, int agentId, AgentCommandCompleteRequest request, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var command = await db.Set<AgentCommand>().FirstOrDefaultAsync(x => x.Id == commandId && x.AgentId == agentId && !x.IsDeleted, cancellationToken);
        if (command is null) throw new BaseException("Komut bulunamadı.", 404);

        var targetStatus = request.Success ? AgentCommandStatus.Completed : AgentCommandStatus.Failed;
        AgentCommandStateMachine.EnforceTransition(command.Status, targetStatus, command.Id);

        var prevStatus = command.Status;
        command.Status = targetStatus;
        command.CompletedAt = DateTime.UtcNow;
        command.ResultPayload = request.ResultPayload;
        command.ErrorCode = request.ErrorCode;
        command.ErrorMessage = request.ErrorMessage;

        db.Set<AgentCommandExecution>().Add(new AgentCommandExecution
        {
            CommandId = command.Id, AgentId = agentId, KurumId = command.KurumId,
            Status = targetStatus.ToString(), PreviousStatus = prevStatus.ToString(),
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

        AgentCommandStateMachine.EnforceTransition(command.Status, AgentCommandStatus.Failed, command.Id);

        command.Status = AgentCommandStatus.Failed;
        command.CompletedAt = DateTime.UtcNow;
        command.ErrorMessage = errorMessage;

        db.Set<AgentCommandExecution>().Add(new AgentCommandExecution
        {
            CommandId = command.Id, AgentId = agentId, KurumId = command.KurumId,
            Status = "Failed", PreviousStatus = command.Status.ToString(),
            ErrorMessage = errorMessage, MachineName = Environment.MachineName,
            CreatedBy = "agent", CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RejectAsync(Guid commandId, int agentId, string errorMessage, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var command = await db.Set<AgentCommand>().FirstOrDefaultAsync(x => x.Id == commandId && x.AgentId == agentId && !x.IsDeleted, cancellationToken);
        if (command is null) throw new BaseException("Komut bulunamadı.", 404);

        AgentCommandStateMachine.EnforceTransition(command.Status, AgentCommandStatus.Rejected, command.Id);

        command.Status = AgentCommandStatus.Rejected;
        command.CompletedAt = DateTime.UtcNow;
        command.ErrorMessage = errorMessage;

        db.Set<AgentCommandExecution>().Add(new AgentCommandExecution
        {
            CommandId = command.Id, AgentId = agentId, KurumId = command.KurumId,
            Status = "Rejected", PreviousStatus = command.Status.ToString(),
            ErrorMessage = errorMessage, MachineName = Environment.MachineName,
            CreatedBy = "agent", CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task ValidateScopeAsync(StysAppDbContext db, int agentId, string commandType, CancellationToken ct)
    {
        if (!CommandScopeMap.TryGetValue(commandType, out var requiredScope)) return;
        var hasScope = await db.Set<AgentScope>().AnyAsync(x => x.AgentId == agentId && x.Scope == requiredScope && x.AktifMi && !x.IsDeleted, ct);
        if (!hasScope) throw new BaseException($"Agent '{requiredScope}' scope'una sahip değil.", 403);
    }

    private static async Task ValidateCapabilityAsync(StysAppDbContext db, int agentId, string commandType, CancellationToken ct)
    {
        if (!CommandCapabilityMap.TryGetValue(commandType, out var requiredCap)) return;
        var hasCap = await db.Set<AgentCapability>().AnyAsync(x => x.AgentId == agentId && x.Capability == requiredCap && x.AktifMi && !x.IsDeleted, ct);
        if (!hasCap) throw new BaseException($"Agent '{requiredCap}' capability'sine sahip değil.", 403);
    }

    private static AgentCommandDto MapToDto(AgentCommand c) => new()
    {
        Id = c.Id, AgentId = c.AgentId, CommandType = c.CommandType, Payload = c.Payload,
        Status = (int)c.Status, Priority = c.Priority, ScheduledAt = c.ScheduledAt,
        ExpiresAt = c.ExpiresAt, RetryCount = c.RetryCount, MaxRetryCount = c.MaxRetryCount,
        CorrelationId = c.CorrelationId, IdempotencyKey = c.IdempotencyKey, CreatedAt = c.CreatedAt ?? DateTime.MinValue
    };
}
