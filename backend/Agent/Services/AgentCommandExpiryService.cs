using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore.Storage;
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
                && x.ExpiresAt.HasValue
                && x.ExpiresAt.Value <= now
                && ExpirableStatuses.Contains(x.Status))
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
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await AcquireAgentExpiryLockAsync(db, agentId, ct);

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
            await tx.CommitAsync(ct);
            return 0;
        }

        foreach (var cmd in timedOutCommands)
        {
            MarkExpired(db, cmd, agentId, utcNow);
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        _logger.LogInformation("Timed out agent commands expired. AgentId={AgentId}, ExpiredCount={ExpiredCount}", agentId, timedOutCommands.Count);
        return timedOutCommands.Count;
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

        if (string.Equals(cmd.CommandType, "PavoStartPayment", StringComparison.OrdinalIgnoreCase)
            || string.Equals(cmd.CommandType, "PavoGetPaymentResult", StringComparison.OrdinalIgnoreCase))
        {
            var paymentId = TryGetPaymentIdFromCommandPayload(cmd.Payload);
            var payment = paymentId.HasValue
                ? db.PosOdemeIslemleri.FirstOrDefault(x => x.Id == paymentId.Value && !x.IsDeleted)
                : null;
            ApplyPavoPaymentExpiry(payment, cmd.CommandType, utcNow);
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
