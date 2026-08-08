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
    private readonly IAgentCommandRealtimeNotifier? _notifier;

    private static readonly Dictionary<string, string> CommandScopeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Ping"] = "agent.command.execute", ["HealthCheck"] = "agent.command.execute",
        ["RefreshConfiguration"] = "agent.config.read", ["PavoConnectionTest"] = "stys.pavo.connection.test"
    };

    private static readonly Dictionary<string, string> CommandCapabilityMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["PavoConnectionTest"] = "pavo"
    };

    public AgentCommandService(IDbContextFactory<StysAppDbContext> dbContextFactory, ICurrentTenantAccessor tenantAccessor, IAgentCommandRealtimeNotifier? notifier = null)
    {
        _dbContextFactory = dbContextFactory; _tenantAccessor = tenantAccessor; _notifier = notifier;
    }

    public async Task<AgentCommandDto> SendAsync(AgentCommandSendRequest request, string requestedBy, CancellationToken ct)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var agent = await db.Set<AgentEntity>().FirstOrDefaultAsync(x => x.Id == request.AgentId && !x.IsDeleted, ct);
        if (agent is null) throw new BaseException("Agent bulunamadı.", 404);
        if (!_tenantAccessor.IsSuperAdmin() && !_tenantAccessor.GetAccessibleKurumIds().Contains(agent.KurumId))
            throw new BaseException("Bu agent'a komut gönderme yetkiniz yok.", 403);
        if (agent.Durum != AgentDurum.Active) throw new BaseException("Agent aktif değil.", 400);
        await ValidateScopeAsync(db, agent.Id, request.CommandType, ct);
        await ValidateCapabilityAsync(db, agent.Id, request.CommandType, ct);

        var cmd = new AgentCommand
        {
            Id = Guid.NewGuid(), AgentId = agent.Id, KurumId = agent.KurumId, CommandType = request.CommandType,
            Payload = request.Payload, Status = AgentCommandStatus.Pending, Priority = request.Priority,
            ExpiresAt = request.ExpirationMinutes.HasValue ? DateTime.UtcNow.AddMinutes(request.ExpirationMinutes.Value) : null,
            MaxRetryCount = request.MaxRetryCount, CorrelationId = Guid.NewGuid().ToString("N"),
            IdempotencyKey = ($"{request.CommandType}:{Guid.NewGuid():N}").Length > 64 ? ($"{request.CommandType}:{Guid.NewGuid():N}")[..64] : $"{request.CommandType}:{Guid.NewGuid():N}",
            RequestedBy = requestedBy, CreatedBy = requestedBy, CreatedAt = DateTime.UtcNow
        };
        db.Set<AgentCommand>().Add(cmd);
        await db.SaveChangesAsync(ct);
        var dto = MapToDto(cmd);
        NotifyIfNeeded(dto);
        return dto;
    }

    public async Task<IReadOnlyCollection<AgentCommandDto>> GetPendingCommandsAsync(int agentId, CancellationToken ct)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var now = DateTime.UtcNow;
        var commands = await db.Set<AgentCommand>()
            .Where(x => x.AgentId == agentId && !x.IsDeleted && x.Status == AgentCommandStatus.Pending && (x.ExpiresAt == null || x.ExpiresAt > now))
            .OrderByDescending(x => x.Priority).ThenBy(x => x.CreatedAt)
            .Take(50)
            .ToListAsync(ct);

        if (commands.Count > 0)
        {
            await db.Set<AgentCommand>()
                .Where(x => commands.Select(c => c.Id).Contains(x.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, AgentCommandStatus.Delivered), ct);
        }

        await tx.CommitAsync(ct);
        return commands.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyCollection<AgentCommandDto>> GetHistoryAsync(int agentId, CancellationToken ct)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        return await db.Set<AgentCommand>().Where(x => x.AgentId == agentId && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt).Take(100)
            .Select(x => new AgentCommandDto { Id = x.Id, AgentId = x.AgentId, CommandType = x.CommandType, Status = (int)x.Status, Priority = x.Priority, ScheduledAt = x.ScheduledAt, ExpiresAt = x.ExpiresAt, RetryCount = x.RetryCount, MaxRetryCount = x.MaxRetryCount, CorrelationId = x.CorrelationId, IdempotencyKey = x.IdempotencyKey, CreatedAt = x.CreatedAt ?? DateTime.MinValue })
            .ToListAsync(ct);
    }

    public async Task AcceptAsync(Guid commandId, int agentId, CancellationToken ct)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var cmd = await db.Set<AgentCommand>().FirstOrDefaultAsync(x => x.Id == commandId && x.AgentId == agentId && !x.IsDeleted, ct);
        if (cmd is null) throw new BaseException("Komut bulunamadı.", 404);
        AgentCommandStateMachine.EnforceTransition(cmd.Status, AgentCommandStatus.Accepted, cmd.Id);
        if (cmd.ExpiresAt.HasValue && DateTime.UtcNow > cmd.ExpiresAt.Value)
        {
            cmd.Status = AgentCommandStatus.Expired;
            await db.SaveChangesAsync(ct);
            throw new BaseException("Komut süresi dolmuş.", 400);
        }

        var prev = cmd.Status;
        cmd.Status = AgentCommandStatus.Accepted;
        cmd.StartedAt = DateTime.UtcNow;
        AddExecution(db, cmd, "Accepted", prev, agentId);
        await db.SaveChangesAsync(ct);
        NotifyIfNeeded(MapToDto(cmd));
    }    public async Task SetRunningAsync(Guid commandId, int agentId, CancellationToken ct)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var cmd = await db.Set<AgentCommand>().FirstOrDefaultAsync(x => x.Id == commandId && x.AgentId == agentId && !x.IsDeleted, ct);
        if (cmd is null) throw new BaseException("Komut bulunamadı.", 404);
        AgentCommandStateMachine.EnforceTransition(cmd.Status, AgentCommandStatus.Running, cmd.Id);

        var prev = cmd.Status;
        cmd.Status = AgentCommandStatus.Running;
        AddExecution(db, cmd, "Running", prev, agentId);
        await db.SaveChangesAsync(ct);
        NotifyIfNeeded(MapToDto(cmd));
    }    public async Task CompleteAsync(Guid commandId, int agentId, AgentCommandCompleteRequest request, CancellationToken ct)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var cmd = await db.Set<AgentCommand>().FirstOrDefaultAsync(x => x.Id == commandId && x.AgentId == agentId && !x.IsDeleted, ct);
        if (cmd is null) throw new BaseException("Komut bulunamadı.", 404);

        var target = request.Success ? AgentCommandStatus.Completed : AgentCommandStatus.Failed;
        AgentCommandStateMachine.EnforceTransition(cmd.Status, target, cmd.Id);

        var prev = cmd.Status;
        cmd.Status = target;
        cmd.CompletedAt = DateTime.UtcNow;
        cmd.ResultPayload = request.ResultPayload;
        cmd.ErrorCode = request.ErrorCode;
        cmd.ErrorMessage = request.ErrorMessage;
        AddExecution(db, cmd, target.ToString(), prev, agentId, request.ErrorCode, request.ErrorMessage);
        await db.SaveChangesAsync(ct);
        NotifyIfNeeded(MapToDto(cmd));
    }

    public async Task FailAsync(Guid commandId, int agentId, string errorMessage, CancellationToken ct)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var cmd = await db.Set<AgentCommand>().FirstOrDefaultAsync(x => x.Id == commandId && x.AgentId == agentId && !x.IsDeleted, ct);
        if (cmd is null) throw new BaseException("Komut bulunamadı.", 404);
        AgentCommandStateMachine.EnforceTransition(cmd.Status, AgentCommandStatus.Failed, cmd.Id);

        var prev = cmd.Status;
        cmd.Status = AgentCommandStatus.Failed;
        cmd.CompletedAt = DateTime.UtcNow;
        cmd.ErrorMessage = errorMessage;
        AddExecution(db, cmd, "Failed", prev, agentId, errorMessage: errorMessage);
        await db.SaveChangesAsync(ct);
        NotifyIfNeeded(MapToDto(cmd));
    }

    public async Task RejectAsync(Guid commandId, int agentId, string errorMessage, CancellationToken ct)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var cmd = await db.Set<AgentCommand>().FirstOrDefaultAsync(x => x.Id == commandId && x.AgentId == agentId && !x.IsDeleted, ct);
        if (cmd is null) throw new BaseException("Komut bulunamadı.", 404);
        AgentCommandStateMachine.EnforceTransition(cmd.Status, AgentCommandStatus.Rejected, cmd.Id);

        var prev = cmd.Status;
        cmd.Status = AgentCommandStatus.Rejected;
        cmd.CompletedAt = DateTime.UtcNow;
        cmd.ErrorMessage = errorMessage;
        AddExecution(db, cmd, "Rejected", prev, agentId, errorMessage: errorMessage);
        await db.SaveChangesAsync(ct);
        NotifyIfNeeded(MapToDto(cmd));
    }

    private static void AddExecution(StysAppDbContext db, AgentCommand cmd, string status, AgentCommandStatus prev, int agentId, string? errorCode = null, string? errorMessage = null)
    {
        db.Set<AgentCommandExecution>().Add(new AgentCommandExecution
        {
            CommandId = cmd.Id, AgentId = agentId, KurumId = cmd.KurumId, Status = status,
            PreviousStatus = prev.ToString(), ErrorCode = errorCode, ErrorMessage = errorMessage,
            MachineName = Environment.MachineName, CreatedBy = "agent", CreatedAt = DateTime.UtcNow
        });
    }

    private static async Task ValidateScopeAsync(StysAppDbContext db, int agentId, string commandType, CancellationToken ct)
    {
        if (!CommandScopeMap.TryGetValue(commandType, out var s)) return;
        if (!await db.Set<AgentScope>().AnyAsync(x => x.AgentId == agentId && x.Scope == s && x.AktifMi && !x.IsDeleted, ct))
            throw new BaseException($"Agent '{s}' scope'una sahip değil.", 403);
    }

    private static async Task ValidateCapabilityAsync(StysAppDbContext db, int agentId, string commandType, CancellationToken ct)
    {
        if (!CommandCapabilityMap.TryGetValue(commandType, out var c)) return;
        if (!await db.Set<AgentCapability>().AnyAsync(x => x.AgentId == agentId && x.Capability == c && x.AktifMi && !x.IsDeleted, ct))
            throw new BaseException($"Agent '{c}' capability'sine sahip değil.", 403);
    }

    private void NotifyIfNeeded(AgentCommandDto dto)
    {
        if (_notifier is null) return;
        _ = Task.Run(async () => { try { await _notifier.CommandUpdatedAsync(dto, CancellationToken.None); } catch { } });
    }

    private static AgentCommandDto MapToDto(AgentCommand c) => new()
    {
        Id = c.Id, AgentId = c.AgentId, CommandType = c.CommandType, Payload = c.Payload,
        Status = (int)c.Status, Priority = c.Priority, ScheduledAt = c.ScheduledAt,
        ExpiresAt = c.ExpiresAt, RetryCount = c.RetryCount, MaxRetryCount = c.MaxRetryCount,
        CorrelationId = c.CorrelationId, IdempotencyKey = c.IdempotencyKey, CreatedAt = c.CreatedAt ?? DateTime.MinValue
    };
}
