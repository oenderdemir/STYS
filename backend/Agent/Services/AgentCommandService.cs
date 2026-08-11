using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using STYS.Agent.Contracts.Dtos;
using STYS.Agent.Contracts.Enums;
using STYS.Agent.Entities;
using STYS.Entegrasyonlar.Pos.Entities;
using STYS.Infrastructure.EntityFramework;
using System.Text.Json;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;
using AgentEntity = STYS.Agent.Entities.Agent;

namespace STYS.Agent.Services;

public sealed class AgentCommandService
{
    private readonly IDbContextFactory<StysAppDbContext> _dbContextFactory;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly IAgentCommandRealtimeNotifier? _notifier;
    private readonly ILogger<AgentCommandService> _logger;

    private static readonly Dictionary<string, string> CommandScopeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Ping"] = "agent.command.execute", ["HealthCheck"] = "agent.command.execute",
        ["RefreshConfiguration"] = "agent.config.read",
        ["PavoPairing"] = "agent.command.execute",
        ["PavoPing"] = "agent.command.execute",
        ["PavoGetDeviceInfo"] = "agent.command.execute",
        ["PavoStartPayment"] = "agent.command.execute",
        ["PavoGetPaymentResult"] = "agent.command.execute",
        ["PavoConnectionTest"] = "stys.pavo.connection.test"
    };

    private static readonly Dictionary<string, string> CommandCapabilityMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["PavoPairing"] = "pavo",
        ["PavoPing"] = "pavo",
        ["PavoGetDeviceInfo"] = "pavo",
        ["PavoStartPayment"] = "pavo",
        ["PavoGetPaymentResult"] = "pavo",
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
        IAgentCommandRealtimeNotifier? notifier = null)
    {
        _dbContextFactory = dbContextFactory; _tenantAccessor = tenantAccessor; _logger = logger; _notifier = notifier;
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
        await AcquirePollLockAsync(db, agentId, ct);

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
    }

    public async Task SetRunningAsync(Guid commandId, int agentId, CancellationToken ct)
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
    }

    public async Task CompleteAsync(Guid commandId, int agentId, AgentCommandCompleteRequest request, CancellationToken ct)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var cmd = await db.Set<AgentCommand>().FirstOrDefaultAsync(x => x.Id == commandId && x.AgentId == agentId && !x.IsDeleted, ct);
        if (cmd is null) throw new BaseException("Komut bulunamadı.", 404);

        var target = request.Success ? AgentCommandStatus.Completed : AgentCommandStatus.Failed;
        AgentCommandStateMachine.EnforceTransition(cmd.Status, target, cmd.Id);

        var pavoContext = IsPavoCommand(cmd.CommandType)
            ? ResolveValidatedPavoCommandTarget(db, cmd)
            : null;

        var prev = cmd.Status;
        cmd.Status = target;
        cmd.CompletedAt = DateTime.UtcNow;
        cmd.ResultPayload = request.ResultPayload;
        cmd.ErrorCode = request.ErrorCode;
        cmd.ErrorMessage = request.ErrorMessage;
        ApplyPavoCommandResultIfNeeded(db, cmd, request, pavoContext?.Device, pavoContext?.Payment, ct);
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

        var pavoContext = IsPavoCommand(cmd.CommandType)
            ? ResolveValidatedPavoCommandTarget(db, cmd)
            : null;

        var prev = cmd.Status;
        cmd.Status = AgentCommandStatus.Failed;
        cmd.CompletedAt = DateTime.UtcNow;
        cmd.ErrorMessage = errorMessage;
        ApplyPavoCommandResultIfNeeded(db, cmd, new AgentCommandCompleteRequest { Id = commandId, Success = false, ErrorMessage = errorMessage }, pavoContext?.Device, pavoContext?.Payment, ct);
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

        var pavoContext = IsPavoCommand(cmd.CommandType)
            ? ResolveValidatedPavoCommandTarget(db, cmd)
            : null;

        var prev = cmd.Status;
        cmd.Status = AgentCommandStatus.Rejected;
        cmd.CompletedAt = DateTime.UtcNow;
        cmd.ErrorMessage = errorMessage;
        ApplyPavoCommandResultIfNeeded(db, cmd, new AgentCommandCompleteRequest { Id = commandId, Success = false, ErrorMessage = errorMessage }, pavoContext?.Device, pavoContext?.Payment, ct);
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

    private void ApplyPavoCommandResultIfNeeded(
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
                ApplyPavoPaymentResult(db, cmd, request, validatedDevice, validatedPayment);
                return;
            }

            if (!request.Success)
            {
                return;
            }

            switch (cmd.CommandType)
            {
                case "PavoPairing":
                    ApplyPavoPairingResult(db, cmd, validatedDevice, request.ResultPayload);
                    break;
                case "PavoPing":
                    ApplyPavoPingResult(db, validatedDevice);
                    break;
                case "PavoGetDeviceInfo":
                    ApplyPavoGetDeviceInfoResult(db, validatedDevice, request.ResultPayload, ct);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PAVO command sonucu uygulanamadı. CommandType={CommandType}, CommandId={CommandId}", cmd.CommandType, cmd.Id);
        }
    }

    private void ApplyPavoPairingResult(StysAppDbContext db, AgentCommand cmd, PosCihazi? device, string? resultPayload)
    {
        var response = DeserializePayload<PavoPairingResponse>(resultPayload);
        if (response is null || device is null)
        {
            return;
        }

        device.Fingerprint = response.Fingerprint ?? device.Fingerprint;
        device.TargetFingerprint = response.TargetFingerprint ?? device.TargetFingerprint;
        device.PairingId = response.PairingId ?? device.PairingId;
        device.PairingCode = response.PairingCode ?? device.PairingCode;
        device.EslesmeOnayliMi = response.OnayliMi;
        device.SonBaglantiTarihi = DateTime.UtcNow;
    }

    private void ApplyPavoPingResult(StysAppDbContext db, PosCihazi? device)
    {
        if (device is null)
        {
            return;
        }

        device.SonBaglantiTarihi = DateTime.UtcNow;
    }

    private void ApplyPavoGetDeviceInfoResult(StysAppDbContext db, PosCihazi? device, string? resultPayload, CancellationToken ct)
    {
        var response = DeserializePayload<PavoGetDeviceInfoResponse>(resultPayload);
        if (response is null || device is null)
        {
            return;
        }

        device.Fingerprint = response.Fingerprint ?? device.Fingerprint;
        device.TargetFingerprint = response.TargetFingerprint ?? device.TargetFingerprint;
        device.SonBaglantiTarihi = DateTime.UtcNow;

        SyncDiscoveredTerminals(db, device, response.Terminals);
    }

    private void ApplyPavoPaymentResult(
        StysAppDbContext db,
        AgentCommand cmd,
        AgentCommandCompleteRequest request,
        PosCihazi? device,
        PosOdemeIslemi? payment)
    {
        if (device is null || payment is null)
        {
            return;
        }

        PavoPaymentResponseBase? response = cmd.CommandType.Equals("PavoStartPayment", StringComparison.OrdinalIgnoreCase)
            ? DeserializePayload<PavoStartPaymentResponse>(request.ResultPayload)
            : DeserializePayload<PavoGetPaymentResultResponse>(request.ResultPayload);

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

        if (string.Equals(cmd.CommandType, "PavoStartPayment", StringComparison.OrdinalIgnoreCase) || !payment.AgentCommandId.HasValue)
        {
            payment.AgentCommandId = cmd.Id;
        }
        payment.PosTerminalId = terminal.Id;
        payment.AcquirerId = response?.Data?.AcquirerId ?? payment.AcquirerId;
        payment.TerminalId = response?.Data?.TerminalId ?? payment.TerminalId;
        payment.MerchantId = response?.Data?.MerchantId ?? payment.MerchantId;
        payment.PavoResultCode = response?.Data?.ResultCode ?? request.ErrorCode ?? response?.ErrorCode;
        payment.PavoMessage = response?.Data?.Message ?? request.ErrorMessage ?? response?.Message;
        payment.RetrievalReferenceNo = response?.Data?.RetrievalReferenceNo ?? payment.RetrievalReferenceNo;
        payment.AcquirerReference = response?.Data?.AcquirerReference ?? payment.AcquirerReference;
        payment.AuthorizationCode = response?.Data?.AuthorizationCode ?? payment.AuthorizationCode;
        payment.SonSorgulamaTarihi = DateTime.UtcNow;
        payment.SaglayiciDurumKodu = response?.Data?.TransactionStatus ?? payment.SaglayiciDurumKodu;
        payment.SonSaglayiciYaniti = request.ResultPayload;

        if (!request.Success)
        {
            payment.Durum = PosOdemeDurumlari.Unknown;
            payment.HataMesaji = Truncate(response?.Data?.Message ?? request.ErrorMessage ?? response?.Message, 1024);
            payment.TamamlanmaTarihi = null;
            return;
        }

        if (string.Equals(cmd.CommandType, "PavoStartPayment", StringComparison.OrdinalIgnoreCase))
        {
            if (response?.Data?.IsSuccessful == true || response?.Data?.IsPending == true)
            {
                payment.Durum = PosOdemeDurumlari.Processing;
                payment.TamamlanmaTarihi = null;
                payment.HataMesaji = null;
                return;
            }

            if (response?.Data?.IsSuccessful == false && response?.Data is not null && response?.HasAbondon != true && response?.HasError != true)
            {
                payment.Durum = PosOdemeDurumlari.Failed;
                payment.HataMesaji = Truncate(response?.Data?.Message ?? response?.Message, 1024);
                payment.TamamlanmaTarihi = DateTime.UtcNow;
                return;
            }

            payment.Durum = PosOdemeDurumlari.Unknown;
            payment.HataMesaji = Truncate(response?.Data?.Message ?? response?.Message, 1024);
            payment.TamamlanmaTarihi = null;
            return;
        }

        if (response?.Data?.IsSuccessful == true)
        {
            payment.Durum = PosOdemeDurumlari.Successful;
            payment.TamamlanmaTarihi = DateTime.UtcNow;
            payment.HataMesaji = null;
            return;
        }

        if (response?.Data?.IsPending == true)
        {
            payment.Durum = PosOdemeDurumlari.Processing;
            payment.TamamlanmaTarihi = null;
            payment.HataMesaji = null;
            return;
        }

        if (response?.Data?.IsUnknown == true || response?.Data is null || response?.HasAbondon == true || response?.HasError == true)
        {
            payment.Durum = PosOdemeDurumlari.Unknown;
            payment.HataMesaji = Truncate(response?.Data?.Message ?? response?.Message ?? request.ErrorMessage, 1024);
            payment.TamamlanmaTarihi = null;
            return;
        }

        payment.Durum = PosOdemeDurumlari.Failed;
        payment.HataMesaji = Truncate(response?.Data?.Message ?? response?.Message ?? request.ErrorMessage, 1024);
        payment.TamamlanmaTarihi = DateTime.UtcNow;
    }

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
            PosTerminal? terminal;
            terminal = existing.FirstOrDefault(x =>
                string.Equals(x.CanonicalAcquirerId, canonicalAcquirerId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.CanonicalTerminalId, terminalId, StringComparison.OrdinalIgnoreCase));
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
        || string.Equals(commandType, "PavoGetPaymentResult", StringComparison.OrdinalIgnoreCase);

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
        _ = Task.Run(async () => { try { await _notifier.CommandUpdatedAsync(dto, CancellationToken.None); } catch { } });
    }

    private static async Task AcquirePollLockAsync(StysAppDbContext db, int agentId, CancellationToken ct)
    {
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

    private static string NormalizeCanonicalValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();

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

    private static AgentCommandDto MapToDto(AgentCommand c) => new()
    {
        Id = c.Id, AgentId = c.AgentId, CommandType = c.CommandType, Payload = c.Payload,
        Status = (int)c.Status, Priority = c.Priority, ScheduledAt = c.ScheduledAt,
        ExpiresAt = c.ExpiresAt, RetryCount = c.RetryCount, MaxRetryCount = c.MaxRetryCount,
        CorrelationId = c.CorrelationId, IdempotencyKey = c.IdempotencyKey, CreatedAt = c.CreatedAt ?? DateTime.MinValue
    };
}
