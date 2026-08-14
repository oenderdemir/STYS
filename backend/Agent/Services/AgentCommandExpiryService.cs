using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore.Storage;
using STYS.Agent.Contracts.Dtos;
using STYS.Agent.Contracts.Enums;
using STYS.Agent.Entities;
using STYS.Entegrasyonlar.Pos.Dtos;
using STYS.Entegrasyonlar.Pos.Entities;
using STYS.Infrastructure.EntityFramework;
using System.Data;
using System.Data.Common;
using System.Text.Json;

namespace STYS.Agent.Services;

public sealed class AgentCommandExpiryService
{
    private static readonly AgentCommandStatus[] ExpirableStatuses =
    [
        AgentCommandStatus.Pending,
        AgentCommandStatus.Delivered,
        AgentCommandStatus.Accepted,
        AgentCommandStatus.Running
    ];

    private static readonly HashSet<string> LeaseReplaySafeCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "Ping",
        "HealthCheck",
        "RefreshConfiguration",
        "PavoPing",
        "PavoGetDeviceInfo",
        "PavoGetPaymentResult",
        "AgentStageUpgrade"
    };

    private readonly IDbContextFactory<StysAppDbContext> _dbContextFactory;
    private readonly ILogger<AgentCommandExpiryService> _logger;

    public AgentCommandExpiryService(
        IDbContextFactory<StysAppDbContext> dbContextFactory,
        ILogger<AgentCommandExpiryService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task<int> ExpireTimedOutCommandsAsync(CancellationToken ct)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var now = DateTime.UtcNow;
        var agentIds = await db.Set<AgentCommand>()
            .AsNoTracking()
            .Where(x =>
                !x.IsDeleted
                && (
                    (x.ExpiresAt.HasValue && x.ExpiresAt.Value <= now && ExpirableStatuses.Contains(x.Status))
                    || (x.LeaseExpiresAt.HasValue && x.LeaseExpiresAt.Value <= now && IsLeaseRecoverableStatus(x.Status)))
            )
            .Select(x => x.AgentId)
            .Distinct()
            .ToListAsync(ct);

        var expiredCount = 0;
        foreach (var agentId in agentIds)
        {
            expiredCount += await ExpireTimedOutCommandsAsync(agentId, ct);
        }

        return expiredCount;
    }

    public async Task<int> ExpireTimedOutCommandsAsync(int agentId, CancellationToken ct)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var useTransaction = db.Database.IsRelational();
        IDbContextTransaction? tx = null;

        try
        {
            if (useTransaction)
            {
                tx = await db.Database.BeginTransactionAsync(ct);
                await AcquireAgentExpiryLockAsync(db, agentId, ct);
            }

            var utcNow = DateTime.UtcNow;
            var timedOutCommands = await db.Set<AgentCommand>()
                .Where(x =>
                    x.AgentId == agentId
                    && !x.IsDeleted
                    && x.ExpiresAt.HasValue
                    && x.ExpiresAt.Value <= utcNow
                    && ExpirableStatuses.Contains(x.Status))
                .OrderBy(x => x.CreatedAt)
                .ToListAsync(ct);

            if (timedOutCommands.Count == 0)
            {
                var leaseExpiredCommands = await db.Set<AgentCommand>()
                    .Where(x =>
                        x.AgentId == agentId
                        && !x.IsDeleted
                        && x.LeaseExpiresAt.HasValue
                        && x.LeaseExpiresAt.Value <= utcNow
                        && IsLeaseRecoverableStatus(x.Status))
                    .OrderBy(x => x.CreatedAt)
                    .ToListAsync(ct);

                if (leaseExpiredCommands.Count == 0)
                {
                    if (tx is not null)
                    {
                        await tx.CommitAsync(ct);
                    }

                    return 0;
                }

                foreach (var cmd in leaseExpiredCommands)
                {
                    ApplyLeaseExpiration(db, cmd, agentId, utcNow);
                }

                await db.SaveChangesAsync(ct);
                if (tx is not null)
                {
                    await tx.CommitAsync(ct);
                }

                _logger.LogInformation("Timed out agent command leases recovered. AgentId={AgentId}, ExpiredCount={ExpiredCount}", agentId, leaseExpiredCommands.Count);
                return leaseExpiredCommands.Count;
            }

            foreach (var cmd in timedOutCommands)
            {
                MarkExpired(db, cmd, agentId, utcNow);
            }

            var timedOutLeaseCommands = await db.Set<AgentCommand>()
                .Where(x =>
                    x.AgentId == agentId
                    && !x.IsDeleted
                    && x.LeaseExpiresAt.HasValue
                    && x.LeaseExpiresAt.Value <= utcNow
                    && IsLeaseRecoverableStatus(x.Status))
                .OrderBy(x => x.CreatedAt)
                .ToListAsync(ct);

            foreach (var cmd in timedOutLeaseCommands)
            {
                ApplyLeaseExpiration(db, cmd, agentId, utcNow);
            }

            await db.SaveChangesAsync(ct);
            if (tx is not null)
            {
                await tx.CommitAsync(ct);
            }

            var totalCount = timedOutCommands.Count + timedOutLeaseCommands.Count;
            _logger.LogInformation("Timed out agent commands expired. AgentId={AgentId}, ExpiredCount={ExpiredCount}", agentId, totalCount);
            return totalCount;
        }
        finally
        {
            if (tx is not null)
            {
                await tx.DisposeAsync();
            }
        }
    }

    private void MarkExpired(StysAppDbContext db, AgentCommand cmd, int agentId, DateTime utcNow)
    {
        var previous = cmd.Status;
        cmd.Status = AgentCommandStatus.Expired;
        cmd.CompletedAt ??= utcNow;
        AddExecution(db, cmd, "Expired", previous, agentId, errorCode: "COMMAND_EXPIRED", errorMessage: "Komut süresi doldu.");

        if (string.Equals(cmd.CommandType, "PavoPing", StringComparison.OrdinalIgnoreCase))
        {
            var deviceId = TryGetDeviceIdFromCommandPayload(cmd.Payload);
            var device = deviceId.HasValue
                ? db.PosCihazlari.FirstOrDefault(x => x.Id == deviceId.Value && !x.IsDeleted)
                : null;
            ApplyPavoPingExpiry(device, utcNow);
            return;
        }

        if (string.Equals(cmd.CommandType, "PavoStartPayment", StringComparison.OrdinalIgnoreCase))
        {
            HandlePavoStartPaymentTimeout(db, cmd, previous, agentId, utcNow, shouldRetry: false);
            return;
        }

        if (string.Equals(cmd.CommandType, "PavoGetPaymentResult", StringComparison.OrdinalIgnoreCase))
        {
            var paymentId = TryGetPaymentIdFromCommandPayload(cmd.Payload);
            var payment = paymentId.HasValue
                ? db.PosOdemeIslemleri.FirstOrDefault(x => x.Id == paymentId.Value && !x.IsDeleted)
                : null;
            ApplyPavoPaymentExpiry(payment, cmd.CommandType, utcNow);
        }
    }

    private void ApplyLeaseExpiration(StysAppDbContext db, AgentCommand cmd, int agentId, DateTime utcNow)
    {
        var previous = cmd.Status;
        var isDelivered = cmd.Status == AgentCommandStatus.Delivered;
        var isStartPayment = string.Equals(cmd.CommandType, "PavoStartPayment", StringComparison.OrdinalIgnoreCase);
        var isPaymentResult = string.Equals(cmd.CommandType, "PavoGetPaymentResult", StringComparison.OrdinalIgnoreCase);
        var shouldRetry =
            cmd.RetryCount < cmd.MaxRetryCount
            && (
                isDelivered
                || (!isStartPayment && LeaseReplaySafeCommands.Contains(cmd.CommandType) && (cmd.Status == AgentCommandStatus.Accepted || cmd.Status == AgentCommandStatus.Running))
            );

        if (shouldRetry)
        {
            cmd.Status = AgentCommandStatus.Pending;
            cmd.RetryCount++;
            cmd.StartedAt = null;
            cmd.CompletedAt = null;
            cmd.DeliveredAt = null;
            cmd.LeaseToken = null;
            cmd.LeaseExpiresAt = null;
            AddExecution(db, cmd, "Pending", previous, agentId, errorCode: "LEASE_EXPIRED", errorMessage: "Komut lease süresi doldu, yeniden kuyruğa alındı.");
        }
        else
        {
            cmd.Status = AgentCommandStatus.Expired;
            cmd.CompletedAt ??= utcNow;
            cmd.LeaseExpiresAt = null;
            AddExecution(db, cmd, "Expired", previous, agentId, errorCode: "LEASE_EXPIRED", errorMessage: "Komut lease süresi doldu.");
        }

        if (string.Equals(cmd.CommandType, "PavoPing", StringComparison.OrdinalIgnoreCase))
        {
            var deviceId = TryGetDeviceIdFromCommandPayload(cmd.Payload);
            var device = deviceId.HasValue
                ? db.PosCihazlari.FirstOrDefault(x => x.Id == deviceId.Value && !x.IsDeleted)
                : null;
            ApplyPavoPingExpiry(device, utcNow);
            return;
        }

        if (isStartPayment)
        {
            HandlePavoStartPaymentTimeout(db, cmd, previous, agentId, utcNow, shouldRetry);
            return;
        }

        if (isPaymentResult)
        {
            return;
        }
    }

    private void ApplyPavoPingExpiry(PosCihazi? device, DateTime utcNow)
    {
        if (device is null)
        {
            return;
        }

        device.LastHealthCheckAt = utcNow;
        device.LastHealthStatus = PavoDeviceHealthStatus.Timeout;
        device.LastHealthError = "PAVO sağlık kontrolü zaman aşımına uğradı.";
    }

    private void ApplyPavoPaymentExpiry(PosOdemeIslemi? payment, string commandType, DateTime utcNow)
    {
        if (payment is null || IsFinalPaymentState(payment.Durum))
        {
            return;
        }

        payment.Durum = PosOdemeDurumlari.Unknown;
        payment.HataMesaji = string.Equals(commandType, "PavoStartPayment", StringComparison.OrdinalIgnoreCase)
            ? "PAVO ödeme başlatma zaman aşımına uğradı. Sonuç daha sonra GetPaymentResult ile doğrulanmalıdır."
            : "PAVO ödeme sonucu zaman aşımına uğradı. Sonuç daha sonra yeniden doğrulanmalıdır.";
        payment.PavoMessage = payment.HataMesaji;
        payment.SonSorgulamaTarihi = utcNow;
        payment.TamamlanmaTarihi = null;
    }

    private void HandlePavoStartPaymentTimeout(
        StysAppDbContext db,
        AgentCommand cmd,
        AgentCommandStatus originalStatus,
        int agentId,
        DateTime utcNow,
        bool shouldRetry)
    {
        if (originalStatus == AgentCommandStatus.Pending || originalStatus == AgentCommandStatus.Delivered)
        {
            return;
        }

        if (originalStatus != AgentCommandStatus.Accepted && originalStatus != AgentCommandStatus.Running)
        {
            return;
        }

        var paymentId = TryGetPaymentIdFromCommandPayload(cmd.Payload);
        var payment = paymentId.HasValue
            ? db.PosOdemeIslemleri.FirstOrDefault(x => x.Id == paymentId.Value && !x.IsDeleted)
            : null;

        ApplyPavoPaymentExpiry(payment, cmd.CommandType, utcNow);
        if (payment is not null && !shouldRetry)
        {
            EnsurePaymentReconciliationCommand(db, cmd, payment, utcNow);
        }
    }

    private void EnsurePaymentReconciliationCommand(StysAppDbContext db, AgentCommand sourceCommand, PosOdemeIslemi payment, DateTime utcNow)
    {
        if (payment.PosCihaziId is null || payment.PosTerminalId <= 0 || IsFinalPaymentState(payment.Durum))
        {
            return;
        }

        var idempotencyKey = $"pavo-reconcile:{payment.Id}";
        var existing = db.Set<AgentCommand>().FirstOrDefault(x =>
            x.AgentId == sourceCommand.AgentId
            && !x.IsDeleted
            && x.IdempotencyKey == idempotencyKey);
        if (existing is not null)
        {
            return;
        }

        var device = db.PosCihazlari.FirstOrDefault(x => x.Id == payment.PosCihaziId.Value && !x.IsDeleted);
        if (device is null)
        {
            return;
        }

        var payload = JsonSerializer.Serialize(new PavoGetPaymentResultRequest
        {
            PosCihaziId = device.Id,
            PosOdemeIslemiId = payment.Id,
            PosTerminalId = payment.PosTerminalId,
            SaleReference = payment.SaleReference ?? string.Empty,
            IpAddress = device.IpAdresi ?? string.Empty,
            HttpPort = device.HttpPort,
            HttpsPort = device.HttpsPort,
            UseHttps = device.HttpsPort.HasValue,
            TransactionHandle = new PavoTransactionHandle
            {
                SerialNumber = device.SeriNo,
                Fingerprint = device.Fingerprint ?? string.Empty,
                TransactionSequence = 0,
                TransactionDate = utcNow
            }
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        db.Set<AgentCommand>().Add(new AgentCommand
        {
            Id = Guid.NewGuid(),
            AgentId = sourceCommand.AgentId,
            KurumId = sourceCommand.KurumId,
            CommandType = "PavoGetPaymentResult",
            Payload = payload,
            Status = AgentCommandStatus.Pending,
            Priority = 1,
            ExpiresAt = utcNow.AddMinutes(10),
            MaxRetryCount = 3,
            CorrelationId = Guid.NewGuid().ToString("N"),
            IdempotencyKey = idempotencyKey,
            RequestedBy = "system",
            CreatedBy = "system",
            CreatedAt = utcNow
        });
    }

    private static void AddExecution(
        StysAppDbContext db,
        AgentCommand cmd,
        string status,
        AgentCommandStatus previous,
        int agentId,
        string? errorCode = null,
        string? errorMessage = null)
    {
        db.Set<AgentCommandExecution>().Add(new AgentCommandExecution
        {
            CommandId = cmd.Id,
            AgentId = agentId,
            KurumId = cmd.KurumId,
            Status = status,
            PreviousStatus = previous.ToString(),
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            MachineName = Environment.MachineName,
            CreatedBy = "agent",
            CreatedAt = DateTime.UtcNow
        });
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

    private static bool IsFinalPaymentState(string? durum) =>
        string.Equals(durum, PosOdemeDurumlari.Successful, StringComparison.OrdinalIgnoreCase)
        || string.Equals(durum, PosOdemeDurumlari.Failed, StringComparison.OrdinalIgnoreCase)
        || string.Equals(durum, PosOdemeDurumlari.Cancelled, StringComparison.OrdinalIgnoreCase);

    private static bool IsLeaseRecoverableStatus(AgentCommandStatus status) =>
        status == AgentCommandStatus.Delivered
        || status == AgentCommandStatus.Accepted
        || status == AgentCommandStatus.Running;

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static async Task AcquireAgentExpiryLockAsync(StysAppDbContext db, int agentId, CancellationToken ct)
    {
        if (!db.Database.IsRelational())
        {
            return;
        }

        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
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
            resource.Value = $"agent-command-expiry:{agentId}";
            command.Parameters.Add(resource);

            var result = await command.ExecuteScalarAsync(ct);
            if (result is null)
            {
                throw new InvalidOperationException("Agent command expiry lock alınamadı.");
            }

            var code = Convert.ToInt32(result);
            if (code < 0)
            {
                throw new InvalidOperationException($"Agent command expiry lock alınamadı. Code={code}");
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
}
