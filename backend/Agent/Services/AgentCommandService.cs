using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;
using STYS.Agent.Contracts.Dtos;
using STYS.Agent.Contracts.Enums;
using STYS.Agent.Entities;
using STYS.Agent.Options;
using STYS.Entegrasyonlar.Pos.Dtos;
using STYS.Entegrasyonlar.Pos.Entities;
using STYS.Entegrasyonlar.Pos.Services;
using STYS.Infrastructure.EntityFramework;
using System.Text.Json;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;
using AgentEntity = STYS.Agent.Entities.Agent;

namespace STYS.Agent.Services;

public sealed class AgentCommandService
{
    private static readonly TimeSpan DefaultLeaseDuration = TimeSpan.FromMinutes(2);
    private readonly IDbContextFactory<StysAppDbContext> _dbContextFactory;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly IAgentCommandRealtimeNotifier? _notifier;
    private readonly AgentCommandExpiryService _commandExpiryService;
    private readonly ILogger<AgentCommandService> _logger;
    private readonly AgentCompatibilityOptions _compatibilityOptions;
    private readonly IPosReceiptPersistenceService? _posReceiptPersistence;
    private readonly IPosGunSonuSlipPersistenceService? _posGunSonuSlipPersistence;

    private static readonly Dictionary<string, string> CommandScopeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Ping"] = "agent.command.execute", ["HealthCheck"] = "agent.command.execute",
        ["RefreshConfiguration"] = "agent.config.read",
        ["PavoPairing"] = "agent.command.execute",
        ["PavoPing"] = "agent.command.execute",
        ["PavoGetDeviceInfo"] = "agent.command.execute",
        ["PavoStartPayment"] = "agent.command.execute",
        ["PavoGetPaymentResult"] = "agent.command.execute",
        ["PavoPerformEOD"] = "agent.command.execute",
        ["AgentStageUpgrade"] = "agent.command.execute",
        ["AgentApplyUpgrade"] = "agent.command.execute",
        ["PavoConnectionTest"] = "stys.pavo.connection.test"
    };

    private static readonly Dictionary<string, string> CommandCapabilityMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["PavoPairing"] = "pavo",
        ["PavoPing"] = "pavo",
        ["PavoGetDeviceInfo"] = "pavo",
        ["PavoStartPayment"] = "pavo",
        ["PavoGetPaymentResult"] = "pavo",
        ["PavoPerformEOD"] = "pavo",
        ["PavoConnectionTest"] = "pavo"
    };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public AgentCommandService(
        IDbContextFactory<StysAppDbContext> dbContextFactory,
        ICurrentTenantAccessor tenantAccessor,
        ILogger<AgentCommandService> logger,
        IAgentCommandRealtimeNotifier? notifier = null,
        AgentCommandExpiryService? commandExpiryService = null,
        IOptions<AgentCompatibilityOptions>? compatibilityOptions = null,
        IPosReceiptPersistenceService? posReceiptPersistence = null,
        IPosGunSonuSlipPersistenceService? posGunSonuSlipPersistence = null)
    {
        _dbContextFactory = dbContextFactory; _tenantAccessor = tenantAccessor; _logger = logger; _notifier = notifier;
        _commandExpiryService = commandExpiryService ?? new AgentCommandExpiryService(dbContextFactory, NullLogger<AgentCommandExpiryService>.Instance);
        _compatibilityOptions = compatibilityOptions?.Value ?? new AgentCompatibilityOptions();
        _posReceiptPersistence = posReceiptPersistence;
        _posGunSonuSlipPersistence = posGunSonuSlipPersistence;
    }

    public async Task<AgentCommandDto> SendAsync(AgentCommandSendRequest request, string requestedBy, CancellationToken ct)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var agent = await db.Set<AgentEntity>().FirstOrDefaultAsync(x => x.Id == request.AgentId && !x.IsDeleted, ct);
        if (agent is null) throw new BaseException("Agent bulunamadı.", 404);
        if (!_tenantAccessor.IsSuperAdmin() && !_tenantAccessor.GetAccessibleKurumIds().Contains(agent.KurumId))
            throw new BaseException("Bu agent'a komut gönderme yetkiniz yok.", 403);
        if (agent.Durum != AgentDurum.Active) throw new BaseException("Agent aktif değil.", 400);

        await _commandExpiryService.ExpireTimedOutCommandsAsync(agent.Id, ct);

        await EnsurePaymentCommandAllowedAsync(db, agent, request.CommandType, ct);

        var normalizedIdempotencyKey = NormalizeIdempotencyKey(request.IdempotencyKey);
        if (!string.IsNullOrWhiteSpace(normalizedIdempotencyKey))
        {
            var existingByIdempotencyKey = await db.Set<AgentCommand>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.AgentId == agent.Id && x.IdempotencyKey == normalizedIdempotencyKey && !x.IsDeleted, ct);
            if (existingByIdempotencyKey is not null)
            {
                return RedactLeaseToken(MapToDto(existingByIdempotencyKey));
            }
        }

        var isReleaseUpgrade = TryGetReleaseCommandIdentity(request.CommandType, request.Payload, out var releaseIdentity);
        var useTransaction = db.Database.IsRelational();

        if (isReleaseUpgrade)
        {
            IDbContextTransaction? tx = null;
            try
            {
                if (useTransaction)
                {
                    tx = await db.Database.BeginTransactionAsync(ct);
                    await AcquireReleaseCommandLockAsync(db, agent.Id, request.CommandType, releaseIdentity, ct);
                }

                var existingReleaseCommand = await FindActiveReleaseCommandAsync(db, agent.Id, request.CommandType, releaseIdentity, ct);
                if (existingReleaseCommand is not null)
                {
                    if (tx is not null)
                    {
                        await tx.CommitAsync(ct);
                    }

                    return RedactLeaseToken(MapToDto(existingReleaseCommand));
                }

                await ValidateScopeAsync(db, agent.Id, request.CommandType, ct);
                await ValidateCapabilityAsync(db, agent.Id, request.CommandType, ct);

                var releaseCommand = CreateCommand(agent, request, requestedBy, releaseIdentity.ReleaseId);
                db.Set<AgentCommand>().Add(releaseCommand);
                try
                {
                    await db.SaveChangesAsync(ct);
                    if (tx is not null)
                    {
                        await tx.CommitAsync(ct);
                    }

                    var releaseDto = RedactLeaseToken(MapToDto(releaseCommand));
                    NotifyIfNeeded(releaseDto);
                    return releaseDto;
                }
                catch (DbUpdateException ex) when (IsReleaseUpgradeUniqueConflict(ex))
                {
                    if (tx is not null)
                    {
                        await tx.RollbackAsync(ct);
                    }

                    var existingAfterConflict = await FindActiveReleaseCommandAsync(db, agent.Id, request.CommandType, releaseIdentity, ct);
                    if (existingAfterConflict is not null)
                    {
                        return RedactLeaseToken(MapToDto(existingAfterConflict));
                    }

                    throw;
                }
                catch (DbUpdateException ex) when (!string.IsNullOrWhiteSpace(normalizedIdempotencyKey) && IsIdempotencyUniqueConflict(ex))
                {
                    if (tx is not null)
                    {
                        await tx.RollbackAsync(ct);
                    }

                    var existingAfterConflict = await db.Set<AgentCommand>()
                        .AsNoTracking()
                        .FirstOrDefaultAsync(x => x.AgentId == agent.Id && x.IdempotencyKey == normalizedIdempotencyKey && !x.IsDeleted, ct);
                    if (existingAfterConflict is not null)
                    {
                        return RedactLeaseToken(MapToDto(existingAfterConflict));
                    }

                    throw;
                }
            }
            finally
            {
                if (tx is not null)
                {
                    await tx.DisposeAsync();
                }
            }
        }

        await ValidateScopeAsync(db, agent.Id, request.CommandType, ct);
        await ValidateCapabilityAsync(db, agent.Id, request.CommandType, ct);

        var cmd = CreateCommand(agent, request, requestedBy, null);
        db.Set<AgentCommand>().Add(cmd);
        try
        {
            await db.SaveChangesAsync(ct);
            var dto = RedactLeaseToken(MapToDto(cmd));
            NotifyIfNeeded(dto);
            return dto;
        }
        catch (DbUpdateException ex) when (!string.IsNullOrWhiteSpace(normalizedIdempotencyKey) && IsIdempotencyUniqueConflict(ex))
        {
            var existingAfterConflict = await db.Set<AgentCommand>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.AgentId == agent.Id && x.IdempotencyKey == normalizedIdempotencyKey && !x.IsDeleted, ct);
            if (existingAfterConflict is not null)
            {
                return RedactLeaseToken(MapToDto(existingAfterConflict));
            }

            throw;
        }
    }

    private void EnsurePaymentCommandAllowed(AgentEntity agent, string commandType)
    {
        if (!string.Equals(commandType, "PavoStartPayment", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var compatibility = AgentCompatibilityEvaluator.Evaluate(agent.AgentVersion, agent.ContractVersion, _compatibilityOptions);
        if (AgentCompatibilityEvaluator.CanStartPayment(compatibility.CompatibilityStatus))
        {
            return;
        }

        var message = compatibility.CompatibilityStatus switch
        {
            AgentCompatibilityStatus.UpdateRequired => "Agent sürümü PAVO ödemesi için güncellenmeli.",
            AgentCompatibilityStatus.IncompatibleContract => "Agent contract sürümü PAVO ödemesi için uyumsuz.",
            AgentCompatibilityStatus.Unknown => "Agent sürümü PAVO ödemesi için doğrulanamadı.",
            _ => "Agent sürümü PAVO ödemesi için desteklenmiyor."
        };

        throw new BaseException(message, 400);
    }

    private async Task EnsurePaymentCommandAllowedAsync(StysAppDbContext db, AgentEntity agent, string commandType, CancellationToken ct)
    {
        EnsurePaymentCommandAllowed(agent, commandType);

        if (!string.Equals(commandType, "PavoStartPayment", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var hasActiveApplyUpgrade = await db.Set<AgentCommand>()
            .AnyAsync(x =>
                x.AgentId == agent.Id
                && !x.IsDeleted
                && x.CommandType == "AgentApplyUpgrade"
                && (x.Status == AgentCommandStatus.Pending
                    || x.Status == AgentCommandStatus.Delivered
                    || x.Status == AgentCommandStatus.Accepted
                    || x.Status == AgentCommandStatus.Running), ct);

        if (hasActiveApplyUpgrade)
        {
            throw new BaseException("Yükseltme uygulanırken yeni PAVO ödeme başlatılamaz.", 400);
        }
    }

    public async Task<IReadOnlyCollection<AgentCommandDto>> GetPendingCommandsAsync(int agentId, CancellationToken ct)
    {
        await _commandExpiryService.ExpireTimedOutCommandsAsync(agentId, ct);

        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var useTransaction = db.Database.IsRelational();
        IDbContextTransaction? tx = null;
        try
        {
            if (useTransaction)
            {
                tx = await db.Database.BeginTransactionAsync(ct);
                await AcquirePollLockAsync(db, agentId, ct);
            }

            var now = DateTime.UtcNow;
            var activeLease = await db.Set<AgentCommand>()
                .Where(x =>
                    x.AgentId == agentId
                    && !x.IsDeleted
                    && x.LeaseToken != null
                    && x.LeaseExpiresAt.HasValue
                    && x.LeaseExpiresAt.Value > now
                    && (x.Status == AgentCommandStatus.Delivered
                        || x.Status == AgentCommandStatus.Accepted
                        || x.Status == AgentCommandStatus.Running))
                .OrderByDescending(x => x.Priority)
                .ThenBy(x => x.CreatedAt)
                .FirstOrDefaultAsync(ct);

            if (activeLease is not null)
            {
                if (tx is not null)
                {
                    await tx.CommitAsync(ct);
                }

                return Array.Empty<AgentCommandDto>();
            }

            var command = await db.Set<AgentCommand>()
                .Where(x =>
                    x.AgentId == agentId
                    && !x.IsDeleted
                    && x.Status == AgentCommandStatus.Pending
                    && (x.ExpiresAt == null || x.ExpiresAt > now))
                .OrderByDescending(x => x.Priority)
                .ThenBy(x => x.CreatedAt)
                .FirstOrDefaultAsync(ct);

            if (command is null)
            {
                if (tx is not null)
                {
                    await tx.CommitAsync(ct);
                }

                return Array.Empty<AgentCommandDto>();
            }

            command.Status = AgentCommandStatus.Delivered;
            command.DeliveredAt = now;
            command.LeaseToken = Guid.NewGuid().ToString("N");
            command.LeaseExpiresAt = now.Add(DefaultLeaseDuration);
            command.StartedAt = null;
            command.CompletedAt = null;

            await db.SaveChangesAsync(ct);

            if (tx is not null)
            {
                await tx.CommitAsync(ct);
            }
            return [MapToDto(command)];
        }
        finally
        {
            if (tx is not null)
            {
                await tx.DisposeAsync();
            }
        }
    }

    public async Task<IReadOnlyCollection<AgentCommandDto>> GetHistoryAsync(int agentId, CancellationToken ct)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        return await db.Set<AgentCommand>().Where(x => x.AgentId == agentId && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt).Take(100)
            .Select(x => new AgentCommandDto { Id = x.Id, AgentId = x.AgentId, CommandType = x.CommandType, Status = (int)x.Status, Priority = x.Priority, ScheduledAt = x.ScheduledAt, ExpiresAt = x.ExpiresAt, LeaseToken = null, LeaseExpiresAt = null, DeliveredAt = x.DeliveredAt, RetryCount = x.RetryCount, MaxRetryCount = x.MaxRetryCount, CorrelationId = x.CorrelationId, IdempotencyKey = x.IdempotencyKey, ResultPayload = x.ResultPayload, ErrorCode = x.ErrorCode, ErrorMessage = x.ErrorMessage, CreatedAt = x.CreatedAt ?? DateTime.MinValue })
            .ToListAsync(ct);
    }

    public async Task AcceptAsync(Guid commandId, int agentId, string leaseToken, CancellationToken ct)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var cmd = await db.Set<AgentCommand>().FirstOrDefaultAsync(x => x.Id == commandId && x.AgentId == agentId && !x.IsDeleted, ct);
        if (cmd is null) throw new BaseException("Komut bulunamadı.", 404);
        EnsureLeaseOwnership(cmd, leaseToken, requireActiveLease: true);
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
    }

    public async Task AcceptAsync(Guid commandId, int agentId, CancellationToken ct)
    {
        var leaseToken = await GetCurrentLeaseTokenAsync(commandId, agentId, ct);
        await AcceptAsync(commandId, agentId, leaseToken, ct);
    }

    public async Task SetRunningAsync(Guid commandId, int agentId, string leaseToken, CancellationToken ct)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var cmd = await db.Set<AgentCommand>().FirstOrDefaultAsync(x => x.Id == commandId && x.AgentId == agentId && !x.IsDeleted, ct);
        if (cmd is null) throw new BaseException("Komut bulunamadı.", 404);
        EnsureLeaseOwnership(cmd, leaseToken, requireActiveLease: true);
        AgentCommandStateMachine.EnforceTransition(cmd.Status, AgentCommandStatus.Running, cmd.Id);

        var prev = cmd.Status;
        cmd.Status = AgentCommandStatus.Running;
        AddExecution(db, cmd, "Running", prev, agentId);
        await db.SaveChangesAsync(ct);
        NotifyIfNeeded(MapToDto(cmd));
    }

    public async Task SetRunningAsync(Guid commandId, int agentId, CancellationToken ct)
    {
        var leaseToken = await GetCurrentLeaseTokenAsync(commandId, agentId, ct);
        await SetRunningAsync(commandId, agentId, leaseToken, ct);
    }

    public async Task CompleteAsync(Guid commandId, int agentId, AgentCommandCompleteRequest request, CancellationToken ct)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var cmd = await db.Set<AgentCommand>().FirstOrDefaultAsync(x => x.Id == commandId && x.AgentId == agentId && !x.IsDeleted, ct);
        if (cmd is null) throw new BaseException("Komut bulunamadı.", 404);
        EnsureLeaseOwnership(cmd, request.LeaseToken, requireActiveLease: false);
        var prev = cmd.Status;

        if (cmd.Status == AgentCommandStatus.Expired && IsPaymentCommand(cmd.CommandType))
        {
            await HandleExpiredPaymentCommandAsync(db, cmd, prev, agentId, request, ct);
            return;
        }

        var target = request.Success ? AgentCommandStatus.Completed : AgentCommandStatus.Failed;
        if (TryHandleApplyUpgradeSettlement(cmd, target, out var noOp))
        {
            if (noOp)
            {
                return;
            }
        }
        else
        {
            AgentCommandStateMachine.EnforceTransition(cmd.Status, target, cmd.Id);
        }

        var pavoContext = IsPavoCommand(cmd.CommandType)
            ? ResolveValidatedPavoCommandTarget(db, cmd)
            : null;

        cmd.Status = target;
        cmd.CompletedAt ??= DateTime.UtcNow;
        cmd.ResultPayload = request.ResultPayload;
        cmd.ErrorCode = request.ErrorCode;
        cmd.ErrorMessage = request.ErrorMessage;
        var cleanupPaths = await ApplyPavoCommandResultIfNeeded(db, cmd, request, pavoContext?.Device, pavoContext?.Payment, ct);
        AddExecution(db, cmd, cmd.Status.ToString(), prev, agentId, request.ErrorCode, request.ErrorMessage);
        await db.SaveChangesAsync(ct);
        CleanupReceiptFiles(cleanupPaths);
        NotifyIfNeeded(MapToDto(cmd));
    }

    public async Task FailAsync(Guid commandId, int agentId, string errorMessage, CancellationToken ct)
    {
        var leaseToken = await GetCurrentLeaseTokenAsync(commandId, agentId, ct);
        await FailAsync(commandId, agentId, new AgentCommandCompleteRequest { Id = commandId, Success = false, LeaseToken = leaseToken, ErrorMessage = errorMessage }, ct);
    }

    public async Task FailAsync(Guid commandId, int agentId, AgentCommandCompleteRequest request, CancellationToken ct)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var cmd = await db.Set<AgentCommand>().FirstOrDefaultAsync(x => x.Id == commandId && x.AgentId == agentId && !x.IsDeleted, ct);
        if (cmd is null) throw new BaseException("Komut bulunamadı.", 404);
        EnsureLeaseOwnership(cmd, request.LeaseToken, requireActiveLease: false);
        var prev = cmd.Status;

        if (cmd.Status == AgentCommandStatus.Expired && IsPaymentCommand(cmd.CommandType))
        {
            await HandleExpiredPaymentCommandAsync(db, cmd, prev, agentId, request, ct);
            return;
        }

        if (TryHandleApplyUpgradeSettlement(cmd, AgentCommandStatus.Failed, out var noOp))
        {
            if (noOp)
            {
                return;
            }
        }
        else
        {
            AgentCommandStateMachine.EnforceTransition(cmd.Status, AgentCommandStatus.Failed, cmd.Id);
        }

        var pavoContext = IsPavoCommand(cmd.CommandType)
            ? ResolveValidatedPavoCommandTarget(db, cmd)
            : null;

        cmd.Status = AgentCommandStatus.Failed;
        cmd.CompletedAt ??= DateTime.UtcNow;
        cmd.ErrorMessage = request.ErrorMessage;
        var cleanupPaths = await ApplyPavoCommandResultIfNeeded(db, cmd, new AgentCommandCompleteRequest { Id = commandId, Success = false, LeaseToken = request.LeaseToken, ErrorMessage = request.ErrorMessage, ErrorCode = request.ErrorCode, ResultPayload = request.ResultPayload }, pavoContext?.Device, pavoContext?.Payment, ct);
        AddExecution(db, cmd, cmd.Status.ToString(), prev, agentId, errorCode: request.ErrorCode, errorMessage: request.ErrorMessage);
        await db.SaveChangesAsync(ct);
        CleanupReceiptFiles(cleanupPaths);
        NotifyIfNeeded(MapToDto(cmd));
    }

    public async Task RejectAsync(Guid commandId, int agentId, AgentCommandCompleteRequest request, CancellationToken ct)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var cmd = await db.Set<AgentCommand>().FirstOrDefaultAsync(x => x.Id == commandId && x.AgentId == agentId && !x.IsDeleted, ct);
        if (cmd is null) throw new BaseException("Komut bulunamadı.", 404);
        EnsureLeaseOwnership(cmd, request.LeaseToken, requireActiveLease: false);
        var prev = cmd.Status;

        if (cmd.Status == AgentCommandStatus.Expired && IsPaymentCommand(cmd.CommandType))
        {
            await HandleExpiredPaymentCommandAsync(db, cmd, prev, agentId, request, ct);
            return;
        }

        if (TryHandleApplyUpgradeSettlement(cmd, AgentCommandStatus.Rejected, out var noOp))
        {
            if (noOp)
            {
                return;
            }
        }
        else
        {
            AgentCommandStateMachine.EnforceTransition(cmd.Status, AgentCommandStatus.Rejected, cmd.Id);
        }

        var pavoContext = IsPavoCommand(cmd.CommandType)
            ? ResolveValidatedPavoCommandTarget(db, cmd)
            : null;

        cmd.Status = AgentCommandStatus.Rejected;
        cmd.CompletedAt ??= DateTime.UtcNow;
        cmd.ErrorMessage = request.ErrorMessage;
        var cleanupPaths = await ApplyPavoCommandResultIfNeeded(db, cmd, new AgentCommandCompleteRequest { Id = commandId, Success = false, LeaseToken = request.LeaseToken, ErrorMessage = request.ErrorMessage, ErrorCode = request.ErrorCode, ResultPayload = request.ResultPayload }, pavoContext?.Device, pavoContext?.Payment, ct);
        AddExecution(db, cmd, cmd.Status.ToString(), prev, agentId, errorCode: request.ErrorCode, errorMessage: request.ErrorMessage);
        await db.SaveChangesAsync(ct);
        CleanupReceiptFiles(cleanupPaths);
        NotifyIfNeeded(MapToDto(cmd));
    }

    public async Task RejectAsync(Guid commandId, int agentId, string errorMessage, CancellationToken ct)
    {
        var leaseToken = await GetCurrentLeaseTokenAsync(commandId, agentId, ct);
        await RejectAsync(commandId, agentId, new AgentCommandCompleteRequest { Id = commandId, Success = false, LeaseToken = leaseToken, ErrorMessage = errorMessage }, ct);
    }

    public async Task RenewLeaseAsync(Guid commandId, int agentId, string leaseToken, CancellationToken ct)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var cmd = await db.Set<AgentCommand>().FirstOrDefaultAsync(x => x.Id == commandId && x.AgentId == agentId && !x.IsDeleted, ct);
        if (cmd is null) throw new BaseException("Komut bulunamadı.", 404);

        EnsureLeaseOwnership(cmd, leaseToken, requireActiveLease: true);
        var now = DateTime.UtcNow;
        cmd.LeaseExpiresAt = now.Add(DefaultLeaseDuration);
        await db.SaveChangesAsync(ct);
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

    private static bool TryHandleApplyUpgradeSettlement(AgentCommand cmd, AgentCommandStatus target, out bool noOp)
    {
        noOp = false;
        if (!string.Equals(cmd.CommandType, "AgentApplyUpgrade", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (cmd.Status == AgentCommandStatus.Expired)
        {
            if (target == AgentCommandStatus.Completed || target == AgentCommandStatus.Failed)
            {
                cmd.Status = target;
                return true;
            }

            noOp = true;
            return true;
        }

        if (cmd.Status == AgentCommandStatus.Completed || cmd.Status == AgentCommandStatus.Failed)
        {
            noOp = true;
            return true;
        }

        return false;
    }

    private async Task HandleExpiredPaymentCommandAsync(
        StysAppDbContext db,
        AgentCommand cmd,
        AgentCommandStatus prev,
        int agentId,
        AgentCommandCompleteRequest request,
        CancellationToken ct)
    {
        var pavoContext = IsPavoCommand(cmd.CommandType)
            ? ResolveValidatedPavoCommandTarget(db, cmd)
            : null;

        cmd.ResultPayload = request.ResultPayload;
        cmd.ErrorCode = request.ErrorCode;
        cmd.ErrorMessage = request.ErrorMessage;
        var cleanupPaths = await ApplyPavoCommandResultIfNeeded(db, cmd, request, pavoContext?.Device, pavoContext?.Payment, ct);
        AddExecution(db, cmd, cmd.Status.ToString(), prev, agentId, request.ErrorCode, request.ErrorMessage);
        await db.SaveChangesAsync(ct);
        CleanupReceiptFiles(cleanupPaths);
        NotifyIfNeeded(MapToDto(cmd));
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

    private async Task<IReadOnlyCollection<string>> ApplyPavoCommandResultIfNeeded(
        StysAppDbContext db,
        AgentCommand cmd,
        AgentCommandCompleteRequest request,
        PosCihazi? validatedDevice,
        PosOdemeIslemi? validatedPayment,
        CancellationToken ct)
    {
        try
        {
            if (IsPaymentCommand(cmd.CommandType))
            {
                return await ApplyPavoPaymentResult(db, cmd, request, validatedDevice, validatedPayment, ct);
            }

            switch (cmd.CommandType)
            {
                case "PavoPairing":
                    if (!request.Success)
                    {
                        break;
                    }
                    ApplyPavoPairingResult(db, cmd, validatedDevice, request.ResultPayload);
                    break;
                case "PavoPing":
                    ApplyPavoPingResult(db, validatedDevice, request);
                    break;
                case "PavoGetDeviceInfo":
                    if (!request.Success)
                    {
                        break;
                    }
                    ApplyPavoGetDeviceInfoResult(db, validatedDevice, request.ResultPayload, ct);
                    break;
                case "PavoPerformEOD":
                    return await ApplyPavoPerformEodResult(db, cmd, request, validatedDevice, ct);
            }

            return Array.Empty<string>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PAVO command sonucu uygulanamadı. CommandType={CommandType}, CommandId={CommandId}", cmd.CommandType, cmd.Id);
            return Array.Empty<string>();
        }
    }

    private async Task<IReadOnlyCollection<string>> ApplyPavoPerformEodResult(
        StysAppDbContext db,
        AgentCommand cmd,
        AgentCommandCompleteRequest request,
        PosCihazi? device,
        CancellationToken ct)
    {
        if (device is null)
        {
            return Array.Empty<string>();
        }

        var eodId = TryGetEodIdFromCommandPayload(cmd.Payload);
        if (eodId is null)
        {
            return Array.Empty<string>();
        }

        var eod = await db.PosGunSonuIslemleri.FirstOrDefaultAsync(x => x.Id == eodId && !x.IsDeleted, ct);
        if (eod is null)
        {
            return Array.Empty<string>();
        }

        // The result must belong to the same device/tesis/kurum and the same command.
        if (eod.KurumId != device.KurumId || eod.TesisId != device.TesisId || eod.PosCihaziId != device.Id || eod.AgentCommandId != cmd.Id)
        {
            _logger.LogWarning("PAVO EOD sonucu eşleşmeyen kayıt için yok sayıldı. PosGunSonuIslemiId={EodId}, CommandId={CommandId}", eodId, cmd.Id);
            return Array.Empty<string>();
        }

        var response = DeserializePayload<PavoPerformEodResponse>(request.ResultPayload);

        // Slip persistence is best-effort and happens for both success and failure responses.
        var cleanupPaths = _posGunSonuSlipPersistence is not null && response?.Data?.EodImage is not null
            ? await _posGunSonuSlipPersistence.PersistAsync(db, eod, response.Data.EodImage, ct)
            : Array.Empty<string>();

        // Raw Base64 (eodImage) and cardNo must never persist centrally.
        var sanitizedPayload = PavoEodSanitizer.Sanitize(request.ResultPayload);
        cmd.ResultPayload = sanitizedPayload ?? request.ResultPayload;

        eod.TamamlanmaTarihi = DateTime.UtcNow;

        var success = response is not null
            && request.Success
            && response.HttpSuccess
            && !response.HasError
            && !response.HasAbondon
            && response.TransactionHandle is not null
            && string.Equals(response.TransactionHandle.SerialNumber, device.SeriNo, StringComparison.OrdinalIgnoreCase);

        if (success)
        {
            eod.Durum = PosGunSonuDurumu.Successful;
            eod.GunSonuMesaji = response!.Data?.GunSonu;
            eod.BatchNo = response.Data?.BatchNo?.ToString(CultureInfo.InvariantCulture);
            eod.EodDateTime = response.Data?.ResultDate;
            eod.EodDataJson = PavoEodSanitizer.SanitizeEodData(response.Data?.EodData);
            eod.PavoErrorCode = null;
            eod.PavoMessage = null;
        }
        else if (response is not null)
        {
            // A response was received but it was a business failure or a device mismatch.
            eod.Durum = PosGunSonuDurumu.Failed;
            eod.PavoErrorCode = request.ErrorCode ?? response.ErrorCode?.ToString(CultureInfo.InvariantCulture);
            eod.PavoMessage = request.ErrorMessage ?? response.Message;
        }
        else
        {
            // No response body: transport failure. Only a never-reached device is a confident failure;
            // a sent-but-unanswered request stays Unknown because the device may have run the EOD.
            var neverReached = PavoDeviceReachability.IsDeviceNeverReached(request.ErrorCode);
            eod.Durum = neverReached ? PosGunSonuDurumu.Failed : PosGunSonuDurumu.Unknown;
            eod.PavoErrorCode = request.ErrorCode;
            eod.PavoMessage = request.ErrorMessage;
        }

        return cleanupPaths;
    }

    private static int? TryGetEodIdFromCommandPayload(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(payload);
            if (doc.RootElement.TryGetProperty("posGunSonuIslemiId", out var prop) && prop.TryGetInt32(out var id))
            {
                return id;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private void ApplyPavoPairingResult(StysAppDbContext db, AgentCommand cmd, PosCihazi? device, string? resultPayload)
    {
        var response = DeserializePayload<PavoPairingResponse>(resultPayload);
        if (response is null || device is null)
        {
            return;
        }

        // The client fingerprint is the agent's own stable, configured value; it must not drift on
        // every re-pair based on whatever the device echoes back. The response TransactionHandle is
        // remote/device metadata, so it is only used to seed the central mirror the first time we
        // have nothing at all - never to overwrite an existing value.
        if (string.IsNullOrWhiteSpace(device.Fingerprint) && !string.IsNullOrWhiteSpace(response.TransactionHandle?.Fingerprint))
        {
            device.Fingerprint = response.TransactionHandle.Fingerprint;
        }

        // TargetFingerprint/PairingId/PairingCode do not exist in the verified reference contract,
        // so nothing is written to them from a pairing response any more.
        // This method only runs when the agent already reported command Success = true, which the
        // agent determines via the reference success criteria (!HasError && !HasAbondon &&
        // (ErrorCode == null || ErrorCode == 0)).
        device.EslesmeOnayliMi = true;
        device.SonBaglantiTarihi = DateTime.UtcNow;
    }

    private void ApplyPavoPingResult(StysAppDbContext db, PosCihazi? device, AgentCommandCompleteRequest request)
    {
        if (device is null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        device.LastHealthCheckAt = now;

        if (request.Success)
        {
            device.LastHealthSuccessAt = now;
            device.LastHealthStatus = PavoDeviceHealthStatus.Healthy;
            device.LastHealthError = null;
            device.SonBaglantiTarihi = now;
            return;
        }

        device.LastHealthStatus = MapHealthStatus(request.ErrorCode, request.ErrorMessage);
        device.LastHealthError = Truncate(SafeHealthError(request.ErrorMessage ?? request.ErrorCode), 1024);
    }

    private void ApplyPavoGetDeviceInfoResult(StysAppDbContext db, PosCihazi? device, string? resultPayload, CancellationToken ct)
    {
        var response = DeserializePayload<PavoGetDeviceInfoResponse>(resultPayload);
        if (response is null || device is null)
        {
            return;
        }

        // device.Fingerprint is the agent's stable client fingerprint (see ApplyPavoPairingResult);
        // GetDeviceInfo's response.Fingerprint is a legacy/diagnostic device echo and must never
        // overwrite it. TargetFingerprint has no such conflict and stays diagnostic metadata.
        device.TargetFingerprint = response.TargetFingerprint ?? device.TargetFingerprint;
        device.SonBaglantiTarihi = DateTime.UtcNow;

        SyncDiscoveredTerminals(db, device, response.Terminals);
    }

    private async Task<IReadOnlyCollection<string>> ApplyPavoPaymentResult(
        StysAppDbContext db,
        AgentCommand cmd,
        AgentCommandCompleteRequest request,
        PosCihazi? device,
        PosOdemeIslemi? payment,
        CancellationToken ct)
    {
        if (device is null || payment is null)
        {
            return Array.Empty<string>();
        }

        var commandType = cmd.CommandType;
        PavoPaymentResponseBase? response = commandType.Equals("PavoStartPayment", StringComparison.OrdinalIgnoreCase)
            ? DeserializePayload<PavoStartPaymentResponse>(request.ResultPayload)
            : DeserializePayload<PavoGetPaymentResultResponse>(request.ResultPayload);

        // Receipt extraction + persistence + sanitization happen for every payment result, BEFORE any
        // final-state short-circuit: a final Successful payment can still recover its missing slip via
        // GetPaymentResult, and the raw receipt Base64 must never persist centrally (AgentCommand
        // ResultPayload / PosOdemeIslemi.SonSaglayiciYaniti).
        var cleanupPaths = _posReceiptPersistence is not null && response?.Data is not null
            ? await _posReceiptPersistence.PersistAsync(db, commandType, payment, response, ct)
            : Array.Empty<string>();

        var sanitizedPayload = PavoReceiptSanitizer.Sanitize(request.ResultPayload);
        cmd.ResultPayload = sanitizedPayload ?? request.ResultPayload;

        var proposedStatus = ResolveProposedPaymentStatus(commandType, request, response);
        var resolvedStatus = ResolveMonotonicPaymentStatus(commandType, payment.Durum, proposedStatus);

        if (IsHardFinalPaymentState(payment.Durum) && string.Equals(resolvedStatus, payment.Durum, StringComparison.OrdinalIgnoreCase))
        {
            return cleanupPaths;
        }

        if (payment.PosCihaziId != device.Id || payment.KurumId != device.KurumId || payment.TesisId != device.TesisId)
        {
            throw new BaseException("PAVO ödeme sonucu farklı bir cihaza ait.", 400);
        }

        var terminal = db.PosTerminaller.FirstOrDefault(x => x.Id == payment.PosTerminalId && !x.IsDeleted)
            ?? throw new BaseException("PAVO ödeme terminali bulunamadı.", 404);
        if (terminal.PosCihaziId != device.Id || terminal.KurumId != device.KurumId || terminal.TesisId != device.TesisId)
        {
            throw new BaseException("PAVO ödeme terminali cihaz kapsamıyla eşleşmiyor.", 400);
        }

        if (payment.SaleReference is null && response?.Data?.SaleReference is not null)
        {
            payment.SaleReference = response.Data.SaleReference;
        }

        if (string.Equals(commandType, "PavoStartPayment", StringComparison.OrdinalIgnoreCase) || !payment.AgentCommandId.HasValue)
        {
            payment.AgentCommandId = cmd.Id;
        }
        payment.PosTerminalId = terminal.Id;
        payment.AcquirerId = response?.Data?.AcquirerId ?? payment.AcquirerId;
        payment.TerminalId = response?.Data?.TerminalId ?? payment.TerminalId;
        payment.MerchantId = response?.Data?.MerchantId ?? payment.MerchantId;
        payment.PavoResultCode = response?.Data?.ResponseCode ?? response?.Data?.ResultCode ?? request.ErrorCode ?? response?.ErrorCode?.ToString(CultureInfo.InvariantCulture);
        payment.PavoMessage = response?.Data?.Message ?? request.ErrorMessage ?? response?.Message;
        payment.RetrievalReferenceNo = response?.Data?.RetrievalReferenceNo ?? payment.RetrievalReferenceNo;
        payment.AcquirerReference = response?.Data?.AcquirerReference ?? payment.AcquirerReference;
        payment.AuthorizationCode = response?.Data?.AuthorizationCode ?? payment.AuthorizationCode;
        payment.SonSorgulamaTarihi = DateTime.UtcNow;
        payment.SaglayiciDurumKodu = response?.Data?.TransactionStatus ?? payment.SaglayiciDurumKodu;
        payment.SonSaglayiciYaniti = sanitizedPayload ?? request.ResultPayload;

        if (!request.Success)
        {
            ApplyResolvedPaymentState(payment, resolvedStatus, response?.Data?.Message ?? request.ErrorMessage ?? response?.Message);
            return cleanupPaths;
        }

        ApplyResolvedPaymentState(payment, resolvedStatus, response?.Data?.Message ?? request.ErrorMessage ?? response?.Message);
        return cleanupPaths;
    }

    /// <summary>Best-effort deletion of replaced receipt files, called only after the caller's
    /// SaveChanges has committed the new metadata (section 8). Old files never get deleted before the
    /// DB points at the new, still-live file.</summary>
    private void CleanupReceiptFiles(IReadOnlyCollection<string>? cleanupPaths)
    {
        if (cleanupPaths is null || cleanupPaths.Count == 0)
        {
            return;
        }

        _posReceiptPersistence?.Cleanup(cleanupPaths);
    }

    private static string ResolveProposedPaymentStatus(string commandType, AgentCommandCompleteRequest request, PavoPaymentResponseBase? response)
    {
        if (string.Equals(commandType, "PavoStartPayment", StringComparison.OrdinalIgnoreCase))
        {
            if (response?.Data?.IsSuccessful == true || response?.Data?.IsPending == true)
            {
                return PosOdemeDurumlari.Processing;
            }

            if (response?.Data?.IsSuccessful == false && response?.Data is not null && response?.HasAbondon != true && response?.HasError != true)
            {
                return PosOdemeDurumlari.Failed;
            }

            // The agent could not open a connection, so the device never saw the request and the
            // card cannot have been charged. Reporting Unknown here would send the operator into a
            // reconciliation loop for a payment that provably never started.
            if (PavoDeviceReachability.IsDeviceNeverReached(request.ErrorCode))
            {
                return PosOdemeDurumlari.Failed;
            }

            return PosOdemeDurumlari.Unknown;
        }

        if (response?.Data?.IsSuccessful == true)
        {
            return PosOdemeDurumlari.Successful;
        }

        if (response?.Data?.IsPending == true)
        {
            return PosOdemeDurumlari.Processing;
        }

        if (response?.Data?.IsUnknown == true || response?.Data is null || response?.HasAbondon == true || response?.HasError == true)
        {
            return PosOdemeDurumlari.Unknown;
        }

        return PosOdemeDurumlari.Failed;
    }

    private static string ResolveMonotonicPaymentStatus(string commandType, string? currentStatus, string proposedStatus)
    {
        if (IsHardFinalPaymentState(currentStatus))
        {
            if (string.Equals(currentStatus, PosOdemeDurumlari.Failed, StringComparison.OrdinalIgnoreCase)
                && string.Equals(commandType, "PavoGetPaymentResult", StringComparison.OrdinalIgnoreCase)
                && string.Equals(proposedStatus, PosOdemeDurumlari.Successful, StringComparison.OrdinalIgnoreCase))
            {
                return PosOdemeDurumlari.Successful;
            }

            return currentStatus!;
        }

        return proposedStatus;
    }

    private static void ApplyResolvedPaymentState(PosOdemeIslemi payment, string resolvedStatus, string? rawMessage)
    {
        payment.Durum = resolvedStatus;
        if (string.Equals(resolvedStatus, PosOdemeDurumlari.Successful, StringComparison.OrdinalIgnoreCase))
        {
            payment.TamamlanmaTarihi = DateTime.UtcNow;
            payment.HataMesaji = null;
            return;
        }

        if (string.Equals(resolvedStatus, PosOdemeDurumlari.Failed, StringComparison.OrdinalIgnoreCase))
        {
            payment.TamamlanmaTarihi = DateTime.UtcNow;
            payment.HataMesaji = Truncate(rawMessage, 1024);
            return;
        }

        payment.TamamlanmaTarihi = null;
        payment.HataMesaji = Truncate(rawMessage, 1024);
    }

    private static bool IsHardFinalPaymentState(string? status) =>
        string.Equals(status, PosOdemeDurumlari.Successful, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, PosOdemeDurumlari.Failed, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, PosOdemeDurumlari.Cancelled, StringComparison.OrdinalIgnoreCase);

    private void SyncDiscoveredTerminals(StysAppDbContext db, PosCihazi device, IReadOnlyCollection<PavoDeviceTerminalInfo> discoveredTerminals)
    {
        var existing = db.PosTerminaller
            .Where(x => x.PosCihaziId == device.Id && !x.IsDeleted)
            .ToList();

        var discoveredIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var terminalInfo in discoveredTerminals)
        {
            if (string.IsNullOrWhiteSpace(terminalInfo.TerminalId))
            {
                continue;
            }

            var terminalId = terminalInfo.TerminalId.Trim();
            var canonicalAcquirerId = NormalizeCanonicalValue(terminalInfo.AcquirerId);
            var canonicalKey = BuildCanonicalTerminalKey(device.Id, canonicalAcquirerId, terminalId);
            discoveredIds.Add(canonicalKey);
            var terminal = existing.FirstOrDefault(x =>
                string.Equals(x.CanonicalAcquirerId, canonicalAcquirerId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.CanonicalTerminalId, terminalId, StringComparison.OrdinalIgnoreCase));

            if (terminal is null)
            {
                terminal = existing.FirstOrDefault(x =>
                    string.Equals(x.SerialNumber, terminalId, StringComparison.OrdinalIgnoreCase));
            }

            if (terminal is null)
            {
                terminal = new PosTerminal
                {
                    KurumId = device.KurumId,
                    TesisId = device.TesisId,
                    PosCihaziId = device.Id,
                    KasaBankaHesapId = null,
                    SaglayiciKodu = "PAVO",
                    Ad = terminalInfo.MerchantId?.Trim().Length > 0 ? terminalInfo.MerchantId!.Trim() : terminalId,
                    SerialNumber = terminalId,
                    CanonicalAcquirerId = canonicalAcquirerId,
                    CanonicalTerminalId = terminalId,
                    SourceTerminalReference = terminalInfo.MerchantId,
                    AcquirerId = terminalInfo.AcquirerId,
                    AcquirerName = terminalInfo.AcquirerName,
                    AktifMi = true,
                    CreatedBy = "agent",
                    CreatedAt = DateTime.UtcNow
                };
                db.PosTerminaller.Add(terminal);
                continue;
            }

            terminal.AcquirerId = terminalInfo.AcquirerId;
            terminal.AcquirerName = terminalInfo.AcquirerName;
            terminal.KurumId = device.KurumId;
            terminal.TesisId = device.TesisId;
            terminal.PosCihaziId = device.Id;
            terminal.CanonicalAcquirerId = canonicalAcquirerId;
            terminal.CanonicalTerminalId = terminalId;
            terminal.SourceTerminalReference = terminalInfo.MerchantId ?? terminal.SourceTerminalReference;
            if (!string.IsNullOrWhiteSpace(terminalInfo.MerchantId))
            {
                terminal.Ad = terminalInfo.MerchantId.Trim();
            }

            terminal.AktifMi = true;
            terminal.IsDeleted = false;
        }

        foreach (var terminal in existing.Where(x => !discoveredIds.Contains(BuildCanonicalTerminalKey(x.PosCihaziId ?? device.Id, x.CanonicalAcquirerId, x.CanonicalTerminalId))))
        {
            terminal.AktifMi = false;
        }
    }

    private PosCihazi ResolveValidatedPavoDeviceForCommand(StysAppDbContext db, AgentCommand cmd)
    {
        var deviceId = TryGetDeviceIdFromCommandPayload(cmd.Payload)
            ?? throw new BaseException("PAVO komut payload'ında cihaz kimliği bulunamadı.", 400);

        var device = db.PosCihazlari.FirstOrDefault(x => x.Id == deviceId && !x.IsDeleted)
            ?? throw new BaseException("PAVO cihazı bulunamadı.", 404);

        if (device.AgentId != cmd.AgentId)
        {
            throw new BaseException("PAVO sonuç hedef agent ile eşleşmiyor.", 400);
        }

        if (device.KurumId != cmd.KurumId)
        {
            throw new BaseException("PAVO sonuç kurum kapsamı ile eşleşmiyor.", 400);
        }

        var agent = db.Set<AgentEntity>().FirstOrDefault(x => x.Id == cmd.AgentId && !x.IsDeleted)
            ?? throw new BaseException("PAVO agent bulunamadı.", 404);

        if (agent.KurumId != device.KurumId)
        {
            throw new BaseException("PAVO sonuç agent kurum kapsamı ile eşleşmiyor.", 400);
        }

        if (agent.Durum != AgentDurum.Active)
        {
            throw new BaseException("PAVO sonuç işlenirken hedef agent aktif değil.", 400);
        }

        var agentTesisBaglantisiVarMi = db.Set<AgentTesis>().Any(x =>
            x.AgentId == agent.Id
            && x.KurumId == device.KurumId
            && x.TesisId == device.TesisId
            && x.AktifMi
            && !x.IsDeleted);

        if (!agentTesisBaglantisiVarMi)
        {
            throw new BaseException("PAVO sonuç işlenirken agent tesis kapsamı geçersiz.", 400);
        }

        return device;
    }

    private PavoCommandValidationContext ResolveValidatedPavoCommandTarget(StysAppDbContext db, AgentCommand cmd)
    {
        var deviceId = TryGetDeviceIdFromCommandPayload(cmd.Payload)
            ?? throw new BaseException("PAVO komut payload'ında cihaz kimliği bulunamadı.", 400);

        var device = db.PosCihazlari.FirstOrDefault(x => x.Id == deviceId && !x.IsDeleted)
            ?? throw new BaseException("PAVO cihazı bulunamadı.", 404);

        if (device.AgentId != cmd.AgentId)
        {
            throw new BaseException("PAVO sonuç hedef agent ile eşleşmiyor.", 400);
        }

        if (device.KurumId != cmd.KurumId)
        {
            throw new BaseException("PAVO sonuç kurum kapsamı ile eşleşmiyor.", 400);
        }

        var agent = db.Set<AgentEntity>().FirstOrDefault(x => x.Id == cmd.AgentId && !x.IsDeleted)
            ?? throw new BaseException("PAVO agent bulunamadı.", 404);

        if (agent.KurumId != device.KurumId)
        {
            throw new BaseException("PAVO sonuç agent kurum kapsamı ile eşleşmiyor.", 400);
        }

        if (agent.Durum != AgentDurum.Active)
        {
            throw new BaseException("PAVO sonuç işlenirken hedef agent aktif değil.", 400);
        }

        var agentTesisBaglantisiVarMi = db.Set<AgentTesis>().Any(x =>
            x.AgentId == agent.Id
            && x.KurumId == device.KurumId
            && x.TesisId == device.TesisId
            && x.AktifMi
            && !x.IsDeleted);

        if (!agentTesisBaglantisiVarMi)
        {
            throw new BaseException("PAVO sonuç işlenirken agent tesis kapsamı geçersiz.", 400);
        }

        PosOdemeIslemi? payment = null;
        if (IsPaymentCommand(cmd.CommandType))
        {
            var paymentId = TryGetPaymentIdFromCommandPayload(cmd.Payload)
                ?? throw new BaseException("PAVO komut payload'ında ödeme kimliği bulunamadı.", 400);
            var payloadSaleReference = TryGetSaleReferenceFromCommandPayload(cmd.Payload);
            var payloadTerminalId = TryGetTerminalIdFromCommandPayload(cmd.Payload);

            payment = db.PosOdemeIslemleri.FirstOrDefault(x => x.Id == paymentId && !x.IsDeleted)
                ?? throw new BaseException("PAVO ödeme kaydı bulunamadı.", 404);

            if (string.Equals(cmd.CommandType, "PavoStartPayment", StringComparison.OrdinalIgnoreCase)
                && payment.AgentCommandId.HasValue
                && payment.AgentCommandId.Value != cmd.Id)
            {
                throw new BaseException("PAVO ödeme sonucu başka bir komuta bağlı.", 400);
            }

            if (payment.PosCihaziId != device.Id || payment.KurumId != device.KurumId || payment.TesisId != device.TesisId)
            {
                throw new BaseException("PAVO ödeme kaydı cihaz/tenant kapsamıyla eşleşmiyor.", 400);
            }

            if (!string.IsNullOrWhiteSpace(payloadSaleReference)
                && !string.Equals(payloadSaleReference, payment.SaleReference, StringComparison.OrdinalIgnoreCase))
            {
                throw new BaseException("PAVO ödeme sale reference doğrulanamadı.", 400);
            }

            if (payloadTerminalId.HasValue && payloadTerminalId.Value != payment.PosTerminalId)
            {
                throw new BaseException("PAVO ödeme terminali doğrulanamadı.", 400);
            }
        }

        return new PavoCommandValidationContext(device, payment);
    }

    private static bool IsPavoCommand(string commandType) =>
        string.Equals(commandType, "PavoPairing", StringComparison.OrdinalIgnoreCase)
        || string.Equals(commandType, "PavoPing", StringComparison.OrdinalIgnoreCase)
        || string.Equals(commandType, "PavoGetDeviceInfo", StringComparison.OrdinalIgnoreCase)
        || string.Equals(commandType, "PavoStartPayment", StringComparison.OrdinalIgnoreCase)
        || string.Equals(commandType, "PavoGetPaymentResult", StringComparison.OrdinalIgnoreCase)
        || string.Equals(commandType, "PavoPerformEOD", StringComparison.OrdinalIgnoreCase);

    private static bool IsPaymentCommand(string commandType) =>
        string.Equals(commandType, "PavoStartPayment", StringComparison.OrdinalIgnoreCase)
        || string.Equals(commandType, "PavoGetPaymentResult", StringComparison.OrdinalIgnoreCase);

    private static T? DeserializePayload<T>(string? payload) where T : class
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        return JsonSerializer.Deserialize<T>(payload, JsonOptions);
    }

    private static int? TryGetDeviceIdFromCommandPayload(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(payload);
            if (TryGetPropertyIgnoreCase(doc.RootElement, "PosCihaziId", out var idElement) && idElement.TryGetInt32(out var id))
            {
                return id;
            }
        }
        catch
        {
        }

        return null;
    }

    private void NotifyIfNeeded(AgentCommandDto dto)
    {
        if (_notifier is null) return;
        var publicDto = RedactLeaseToken(dto);
        _ = Task.Run(async () => { try { await _notifier.CommandUpdatedAsync(publicDto, CancellationToken.None); } catch { } });
    }

    private static async Task AcquirePollLockAsync(StysAppDbContext db, int agentId, CancellationToken ct)
    {
        if (!db.Database.IsRelational())
        {
            return;
        }

        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync(ct);

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = """
                DECLARE @lockResult int;
                EXEC @lockResult = sp_getapplock
                    @Resource = @resource,
                    @LockMode = 'Exclusive',
                    @LockOwner = 'Transaction',
                    @LockTimeout = 10000;
                SELECT @lockResult;
                """;
            var resource = command.CreateParameter();
            resource.ParameterName = "@resource";
            resource.Value = $"agent-command-poll:{agentId}";
            command.Parameters.Add(resource);

            var result = await command.ExecuteScalarAsync(ct);
            if (result is null)
                throw new InvalidOperationException("Agent command poll lock alınamadı.");

            var code = Convert.ToInt32(result);
            if (code < 0)
                throw new InvalidOperationException($"Agent command poll lock alınamadı. Code={code}");
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private static int? TryGetPaymentIdFromCommandPayload(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(payload);
            if (TryGetPropertyIgnoreCase(doc.RootElement, "PosOdemeIslemiId", out var idElement) && idElement.TryGetInt32(out var id))
            {
                return id;
            }
        }
        catch
        {
        }

        return null;
    }

    private static string? TryGetSaleReferenceFromCommandPayload(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(payload);
            if (TryGetPropertyIgnoreCase(doc.RootElement, "SaleReference", out var value))
            {
                return value.GetString();
            }
        }
        catch
        {
        }

        return null;
    }

    private static int? TryGetTerminalIdFromCommandPayload(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(payload);
            if (TryGetPropertyIgnoreCase(doc.RootElement, "PosTerminalId", out var idElement) && idElement.TryGetInt32(out var id))
            {
                return id;
            }
        }
        catch
        {
        }

        return null;
    }

    private static bool TryGetReleaseCommandIdentity(string? commandType, string? payload, out ReleaseCommandIdentity identity)
    {
        identity = default;

        if (!string.Equals(commandType, "AgentStageUpgrade", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(commandType, "AgentApplyUpgrade", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var request = DeserializePayload<AgentStageUpgradeRequest>(payload);
        if (request is null)
        {
            return false;
        }

        var releaseId = request.ReleaseId;
        var version = request.Version?.Trim();
        var runtimeIdentifier = request.RuntimeIdentifier?.Trim();
        if (releaseId <= 0 || string.IsNullOrWhiteSpace(version) || string.IsNullOrWhiteSpace(runtimeIdentifier))
        {
            return false;
        }

        identity = new ReleaseCommandIdentity(releaseId, version, runtimeIdentifier);
        return true;
    }

    private async Task<AgentCommand?> FindActiveReleaseCommandAsync(
        StysAppDbContext db,
        int agentId,
        string commandType,
        ReleaseCommandIdentity identity,
        CancellationToken cancellationToken)
    {
        var activeStatuses = new[]
        {
            AgentCommandStatus.Pending,
            AgentCommandStatus.Delivered,
            AgentCommandStatus.Accepted,
            AgentCommandStatus.Running
        };

        var candidates = await db.Set<AgentCommand>()
            .Where(x => x.AgentId == agentId
                && !x.IsDeleted
                && x.CommandType == commandType
                && activeStatuses.Contains(x.Status))
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return candidates.FirstOrDefault(candidate =>
            TryGetReleaseCommandIdentity(candidate.CommandType, candidate.Payload, out var existing) && existing.Equals(identity));
    }

    private static AgentCommand CreateCommand(AgentEntity agent, AgentCommandSendRequest request, string requestedBy, int? releaseId)
    {
        var now = DateTime.UtcNow;
        var idempotencyKey = NormalizeIdempotencyKey(request.IdempotencyKey)
            ?? Truncate($"{request.CommandType}:{Guid.NewGuid():N}", 128) ?? Guid.NewGuid().ToString("N");
        return new AgentCommand
        {
            Id = Guid.NewGuid(),
            AgentId = agent.Id,
            KurumId = agent.KurumId,
            ReleaseId = releaseId,
            CommandType = request.CommandType,
            Payload = request.Payload,
            Status = AgentCommandStatus.Pending,
            Priority = request.Priority,
            ExpiresAt = request.ExpirationMinutes.HasValue ? now.AddMinutes(request.ExpirationMinutes.Value) : null,
            MaxRetryCount = request.MaxRetryCount,
            CorrelationId = Guid.NewGuid().ToString("N"),
            IdempotencyKey = idempotencyKey,
            RequestedBy = requestedBy,
            CreatedBy = requestedBy,
            CreatedAt = now
        };
    }

    private static bool IsReleaseUpgradeUniqueConflict(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("IX_AgentCommands_AgentId_CommandType_ReleaseId", StringComparison.OrdinalIgnoreCase)
            || message.Contains("AgentStageUpgrade", StringComparison.OrdinalIgnoreCase)
            || message.Contains("AgentApplyUpgrade", StringComparison.OrdinalIgnoreCase)
            || message.Contains("duplicate", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task AcquireReleaseCommandLockAsync(StysAppDbContext db, int agentId, string commandType, ReleaseCommandIdentity identity, CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(ct);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = """
                DECLARE @lockResult int;
                EXEC @lockResult = sp_getapplock
                    @Resource = @resource,
                    @LockMode = 'Exclusive',
                    @LockOwner = 'Transaction',
                    @LockTimeout = 10000;
                SELECT @lockResult;
                """;
            var resource = command.CreateParameter();
            resource.ParameterName = "@resource";
            resource.Value = $"agent-release-command:{commandType}:{agentId}:{identity.ReleaseId}";
            command.Parameters.Add(resource);

            var result = await command.ExecuteScalarAsync(ct);
            if (result is null)
            {
                throw new InvalidOperationException("Agent stage upgrade lock alınamadı.");
            }

            var code = Convert.ToInt32(result);
            if (code < 0)
            {
                throw new InvalidOperationException($"Agent stage upgrade lock alınamadı. Code={code}");
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static string NormalizeCanonicalValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();

    private static PavoDeviceHealthStatus MapHealthStatus(string? errorCode, string? message)
    {
        if (string.IsNullOrWhiteSpace(errorCode))
        {
            return IsTimeoutLike(errorCode, message) ? PavoDeviceHealthStatus.Timeout : PavoDeviceHealthStatus.ProtocolError;
        }

        return errorCode.Trim().ToUpperInvariant() switch
        {
            "TIMEOUT" => PavoDeviceHealthStatus.Timeout,
            "NETWORK" or "NETWORK_UNREACHABLE" or "CONNECTION_REFUSED" or "HOST_UNREACHABLE" => PavoDeviceHealthStatus.Unreachable,
            "TLS_CERTIFICATE" or "TLS" or "TLS_ERROR" => PavoDeviceHealthStatus.TlsError,
            "INVALID_RESPONSE" or "PROTOCOL" or "HTTP_ERROR" or "HANDLER_EXCEPTION" => PavoDeviceHealthStatus.ProtocolError,
            _ when IsTimeoutLike(errorCode, message) => PavoDeviceHealthStatus.Timeout,
            _ => PavoDeviceHealthStatus.ProtocolError
        };
    }

    private static string SafeHealthError(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "PAVO sağlık kontrolü başarısız oldu.";
        }

        return value.Trim();
    }

    private static string BuildCanonicalTerminalKey(int deviceId, string? acquirerId, string terminalId) =>
        $"{deviceId}:{NormalizeCanonicalValue(acquirerId)}:{terminalId.Trim()}";

    private static bool IsTimeoutLike(string? errorCode, string? message) =>
        string.Equals(errorCode, "TIMEOUT", StringComparison.OrdinalIgnoreCase)
        || string.Equals(errorCode, "NETWORK", StringComparison.OrdinalIgnoreCase)
        || string.Equals(errorCode, "CONNECTION_REFUSED", StringComparison.OrdinalIgnoreCase)
        || (!string.IsNullOrWhiteSpace(message) && message.Contains("timeout", StringComparison.OrdinalIgnoreCase));

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string? Truncate(string? value, int maxLength) =>
        string.IsNullOrEmpty(value) || value.Length <= maxLength ? value : value[..maxLength];

    private sealed record PavoCommandValidationContext(PosCihazi Device, PosOdemeIslemi? Payment);

    private readonly record struct ReleaseCommandIdentity(int ReleaseId, string Version, string RuntimeIdentifier);

    private static AgentCommandDto MapToDto(AgentCommand c) => new()
    {
        Id = c.Id, AgentId = c.AgentId, CommandType = c.CommandType, Payload = c.Payload,
        Status = (int)c.Status, Priority = c.Priority, ScheduledAt = c.ScheduledAt,
        ExpiresAt = c.ExpiresAt, RetryCount = c.RetryCount, MaxRetryCount = c.MaxRetryCount,
        LeaseToken = c.LeaseToken, LeaseExpiresAt = c.LeaseExpiresAt, DeliveredAt = c.DeliveredAt,
        CorrelationId = c.CorrelationId, IdempotencyKey = c.IdempotencyKey, ResultPayload = c.ResultPayload,
        ErrorCode = c.ErrorCode, ErrorMessage = c.ErrorMessage, CreatedAt = c.CreatedAt ?? DateTime.MinValue
    };

    private static void EnsureLeaseOwnership(AgentCommand cmd, string? leaseToken, bool requireActiveLease)
    {
        var normalizedLeaseToken = NormalizeLeaseToken(leaseToken);
        var storedLeaseToken = NormalizeLeaseToken(cmd.LeaseToken);
        if (string.IsNullOrWhiteSpace(normalizedLeaseToken) || string.IsNullOrWhiteSpace(storedLeaseToken) || !string.Equals(normalizedLeaseToken, storedLeaseToken, StringComparison.OrdinalIgnoreCase))
        {
            throw new BaseException("Komut lease doğrulaması başarısız.", 409);
        }

        if (!requireActiveLease)
        {
            return;
        }

        if (!cmd.LeaseExpiresAt.HasValue || cmd.LeaseExpiresAt.Value <= DateTime.UtcNow)
        {
            throw new BaseException("Komut lease süresi dolmuş.", 409);
        }
    }

    private async Task<string> GetCurrentLeaseTokenAsync(Guid commandId, int agentId, CancellationToken ct)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var cmd = await db.Set<AgentCommand>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == commandId && x.AgentId == agentId && !x.IsDeleted, ct);
        if (cmd is null)
        {
            throw new BaseException("Komut bulunamadı.", 404);
        }

        if (string.IsNullOrWhiteSpace(cmd.LeaseToken))
        {
            throw new BaseException("Komut lease doğrulaması başarısız.", 409);
        }

        return cmd.LeaseToken;
    }

    private static string? NormalizeLeaseToken(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeIdempotencyKey(string? value)
    {
        var normalized = NormalizeLeaseToken(value);
        return string.IsNullOrWhiteSpace(normalized) ? null : Truncate(normalized, 128);
    }

    private static AgentCommandDto RedactLeaseToken(AgentCommandDto dto) => new()
    {
        Id = dto.Id,
        AgentId = dto.AgentId,
        CommandType = dto.CommandType,
        Payload = dto.Payload,
        Status = dto.Status,
        Priority = dto.Priority,
        ScheduledAt = dto.ScheduledAt,
        ExpiresAt = dto.ExpiresAt,
        LeaseToken = null,
        LeaseExpiresAt = null,
        DeliveredAt = dto.DeliveredAt,
        RetryCount = dto.RetryCount,
        MaxRetryCount = dto.MaxRetryCount,
        CorrelationId = dto.CorrelationId,
        IdempotencyKey = dto.IdempotencyKey,
        ResultPayload = dto.ResultPayload,
        CreatedAt = dto.CreatedAt
    };

    private static bool IsIdempotencyUniqueConflict(DbUpdateException ex)
    {
        return ex.InnerException is Microsoft.Data.SqlClient.SqlException sqlEx
            && (sqlEx.Number == 2601 || sqlEx.Number == 2627);
    }
}
