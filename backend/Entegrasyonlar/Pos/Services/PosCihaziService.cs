using AutoMapper;
using System.Data;
using Microsoft.EntityFrameworkCore;
using STYS.Agent.Authorization;
using STYS.Agent.Contracts.Dtos;
using STYS.Agent.Contracts.Enums;
using STYS.Agent.Entities;
using STYS.Agent.Services;
using STYS.Entegrasyonlar.Pos.Dtos;
using STYS.Entegrasyonlar.Pos.Entities;
using STYS.Entegrasyonlar.Pos.Repositories;
using STYS.Infrastructure.EntityFramework;
using STYS.Tesisler.Entities;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;
using AgentEntity = STYS.Agent.Entities.Agent;

namespace STYS.Entegrasyonlar.Pos.Services;

public sealed class PosCihaziService : BaseRdbmsService<PosCihaziDto, PosCihazi, int>, IPosCihaziService
{
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly ICurrentAgentContext _agentContext;
    private readonly StysAppDbContext _db;
    private readonly IPosCihaziRepository _cihazRepository;
    private readonly AgentCommandService _agentCommandService;

    public PosCihaziService(
        IPosCihaziRepository repository,
        IMapper mapper,
        ICurrentTenantAccessor tenantAccessor,
        ICurrentAgentContext agentContext,
        StysAppDbContext db,
        AgentCommandService agentCommandService)
        : base(repository, mapper)
    {
        _tenantAccessor = tenantAccessor;
        _agentContext = agentContext;
        _db = db;
        _cihazRepository = repository;
        _agentCommandService = agentCommandService;
    }

    public override async Task<PosCihaziDto> AddAsync(PosCihaziDto dto)
    {
        var kurumId = _tenantAccessor.GetCurrentKurumId() ?? throw new BaseException("Aktif kurum seçilmedi.", 400);
        dto.Ad = dto.Ad.Trim();
        dto.SeriNo = dto.SeriNo.Trim();
        dto.KurumId = kurumId;
        await ValidateTesisAsync(dto.TesisId, kurumId);
        if (dto.AgentId.HasValue) await ValidateAgentAsync(dto.AgentId.Value, kurumId, dto.TesisId);
        var duplicate = await _cihazRepository.AnyAsync(x => x.SeriNo == dto.SeriNo && x.KurumId == kurumId && !x.IsDeleted);
        if (duplicate) throw new BaseException("Bu seri numarasına sahip cihaz zaten kayıtlı.", 400);
        var created = await base.AddAsync(dto);
        return await BuildDtoAsync(created.Id!.Value);
    }

    public override async Task<PosCihaziDto> UpdateAsync(PosCihaziDto dto)
    {
        var existing = await _cihazRepository.FirstOrDefaultAsync(x => x.Id == dto.Id && !x.IsDeleted) ?? throw new BaseException("POS cihazı bulunamadı.", 404);
        EnforceKurum(existing.KurumId);
        dto.Ad = dto.Ad.Trim();
        dto.SeriNo = dto.SeriNo.Trim();
        dto.KurumId = existing.KurumId;
        await ValidateTesisAsync(dto.TesisId, existing.KurumId);
        if (dto.AgentId.HasValue) await ValidateAgentAsync(dto.AgentId.Value, existing.KurumId, dto.TesisId);
        var duplicate = await _cihazRepository.AnyAsync(x => x.SeriNo == dto.SeriNo && x.KurumId == existing.KurumId && x.Id != dto.Id && !x.IsDeleted);
        if (duplicate) throw new BaseException("Bu seri numarasına sahip cihaz zaten kayıtlı.", 400);
        await base.UpdateAsync(dto);
        return await BuildDtoAsync(existing.Id);
    }

    public async Task<AgentCommandDto> PairingAsync(int id, string requestedBy, CancellationToken cancellationToken)
    {
        var device = await GetCommandTargetAsync(id, cancellationToken);
        EnsurePavoCommandReady(device);
        var request = BuildPairingRequest(device);
        return await SendCommandAsync(device.AgentId!.Value, "PavoPairing", request, requestedBy, cancellationToken);
    }

    public async Task<AgentCommandDto> PingAsync(int id, string requestedBy, CancellationToken cancellationToken)
    {
        var device = await GetCommandTargetAsync(id, cancellationToken);
        EnsurePavoCommandReady(device);
        var request = BuildPingRequest(device);
        return await SendCommandAsync(device.AgentId!.Value, "PavoPing", request, requestedBy, cancellationToken);
    }

    public async Task<AgentCommandDto> GetDeviceInfoAsync(int id, string requestedBy, CancellationToken cancellationToken)
    {
        var device = await GetCommandTargetAsync(id, cancellationToken);
        EnsurePavoCommandReady(device);
        var request = BuildGetDeviceInfoRequest(device);
        return await SendCommandAsync(device.AgentId!.Value, "PavoGetDeviceInfo", request, requestedBy, cancellationToken);
    }

    public async Task<AgentPavoDeviceRegistrationResult> RegisterFromAgentAsync(AgentPavoDeviceRegisterRequest request, CancellationToken cancellationToken)
    {
        if (!_agentContext.IsAuthenticated)
        {
            throw new BaseException("Agent kimlik doğrulaması gerekli.", 401);
        }

        var agentId = _agentContext.AgentId;
        if (agentId <= 0)
        {
            throw new BaseException("Agent kimliği bulunamadı.", 401);
        }

        if (!request.TesisId.HasValue || request.TesisId.Value <= 0)
        {
            throw new BaseException("Tesis seçimi zorunludur.", 400);
        }

        if (!_agentContext.TesisIds.Contains(request.TesisId.Value))
        {
            throw new BaseException("Seçilen tesis agent kapsamı dışında.", 403);
        }

        if (!string.Equals(request.Provider?.Trim(), "PAVO", StringComparison.OrdinalIgnoreCase))
        {
            throw new BaseException("Sadece PAVO provider kayıt edilebilir.", 400);
        }

        var serialNumber = request.SerialNumber?.Trim();
        if (string.IsNullOrWhiteSpace(serialNumber))
        {
            throw new BaseException("Seri numarası zorunludur.", 400);
        }

        var displayName = request.DisplayName?.Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new BaseException("Cihaz adı zorunludur.", 400);
        }

        var host = request.Host?.Trim();
        if (string.IsNullOrWhiteSpace(host))
        {
            throw new BaseException("Host zorunludur.", 400);
        }

        var agent = await _db.Set<AgentEntity>().FirstOrDefaultAsync(x => x.Id == agentId && !x.IsDeleted, cancellationToken)
            ?? throw new BaseException("Agent bulunamadı.", 404);

        if (agent.KurumId != _agentContext.KurumId)
        {
            throw new BaseException("Agent kurum kapsamı geçersiz.", 403);
        }

        if (agent.Durum != AgentDurum.Active)
        {
            throw new BaseException("Agent aktif değil.", 403);
        }

        var tesisBaglantisiVarMi = await _db.Set<AgentTesis>().AnyAsync(x =>
            x.AgentId == agent.Id
            && x.KurumId == agent.KurumId
            && x.TesisId == request.TesisId.Value
            && x.AktifMi
            && !x.IsDeleted, cancellationToken);

        if (!tesisBaglantisiVarMi)
        {
            throw new BaseException("Seçilen tesis agent kapsamı dışında.", 403);
        }

        var existing = await _db.PosCihazlari
            .Include(x => x.Terminaller)
            .FirstOrDefaultAsync(x => x.SeriNo == serialNumber && x.Saglayici == PosSaglayici.Pavo && !x.IsDeleted, cancellationToken);

        if (existing is not null)
        {
            if (existing.KurumId != agent.KurumId)
            {
                throw new BaseException("Bu seri numaralı cihaz başka kuruma kayıtlı.", 409);
            }

            if (existing.AgentId.HasValue && existing.AgentId.Value != agent.Id)
            {
                throw new BaseException("Bu cihaz başka Agent'a bağlı.", 409);
            }

            if (existing.TesisId != request.TesisId.Value)
            {
                throw new BaseException("Bu cihaz başka tesise bağlı.", 409);
            }
        }

        var now = DateTime.UtcNow;
        var device = existing ?? new PosCihazi
        {
            KurumId = agent.KurumId,
            TesisId = request.TesisId.Value,
            AgentId = agent.Id,
            Saglayici = PosSaglayici.Pavo,
            CreatedBy = "agent",
            CreatedAt = now
        };

        device.KurumId = agent.KurumId;
        device.TesisId = request.TesisId.Value;
        device.AgentId = agent.Id;
        device.Saglayici = PosSaglayici.Pavo;
        device.AgentLocalDeviceId = NormalizeOptional(request.LocalDeviceId);
        device.Ad = displayName;
        device.SeriNo = serialNumber;
        device.IpAdresi = host;
        device.HttpPort = request.HttpPort > 0 ? request.HttpPort : null;
        device.HttpsPort = request.HttpsPort > 0 ? request.HttpsPort : null;
        device.AktifMi = true;
        device.SonBaglantiTarihi = now;
        if (!string.IsNullOrWhiteSpace(request.Fingerprint))
        {
            device.Fingerprint = request.Fingerprint.Trim();
            device.EslesmeOnayliMi = true;
        }
        if (!string.IsNullOrWhiteSpace(request.TargetFingerprint))
        {
            device.TargetFingerprint = request.TargetFingerprint.Trim();
        }
        if (request.TransactionSequence.HasValue)
        {
            device.TransactionSequence = Math.Max(device.TransactionSequence, request.TransactionSequence.Value);
        }

        if (existing is null)
        {
            _db.PosCihazlari.Add(device);
        }

        try
        {
            await ReconcileRegisteredTerminalsAsync(device, request.Terminals, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            var conflict = await _db.PosCihazlari
                .Include(x => x.Terminaller)
                .FirstOrDefaultAsync(x => x.SeriNo == serialNumber && x.Saglayici == PosSaglayici.Pavo && !x.IsDeleted, cancellationToken);

            if (conflict is null)
            {
                throw;
            }

            if (conflict.KurumId != agent.KurumId)
            {
                throw new BaseException("Bu seri numaralı cihaz başka kuruma kayıtlı.", 409);
            }

            if (conflict.AgentId.HasValue && conflict.AgentId.Value != agent.Id)
            {
                throw new BaseException("Bu cihaz başka Agent'a bağlı.", 409);
            }

            if (conflict.TesisId != request.TesisId.Value)
            {
                throw new BaseException("Bu cihaz başka tesise bağlı.", 409);
            }

            conflict.AgentLocalDeviceId = NormalizeOptional(request.LocalDeviceId);
            conflict.Ad = displayName;
            conflict.IpAdresi = host;
            conflict.HttpPort = request.HttpPort > 0 ? request.HttpPort : null;
            conflict.HttpsPort = request.HttpsPort > 0 ? request.HttpsPort : null;
            conflict.AktifMi = true;
            conflict.SonBaglantiTarihi = now;
            if (!string.IsNullOrWhiteSpace(request.Fingerprint))
            {
                conflict.Fingerprint = request.Fingerprint.Trim();
                conflict.EslesmeOnayliMi = true;
            }
            if (!string.IsNullOrWhiteSpace(request.TargetFingerprint))
            {
                conflict.TargetFingerprint = request.TargetFingerprint.Trim();
            }
            if (request.TransactionSequence.HasValue)
            {
                conflict.TransactionSequence = Math.Max(conflict.TransactionSequence, request.TransactionSequence.Value);
            }

            await ReconcileRegisteredTerminalsAsync(conflict, request.Terminals, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            device = conflict;
        }

        return new AgentPavoDeviceRegistrationResult
        {
            CentralPosCihaziId = device.Id,
            ProvisioningStatus = "Provisioned",
            LastProvisionedAt = now,
            ReconciledTerminalCount = device.Terminaller.Count(x => !x.IsDeleted),
            Message = existing is null ? "Cihaz kaydedildi." : "Cihaz güncellendi."
        };
    }

    public async Task<IEnumerable<PosCihaziDto>> GetAllAsync(int? kurumId, int? tesisId, CancellationToken cancellationToken)
    {
        var query = _db.PosCihazlari.AsNoTracking().Where(x => !x.IsDeleted);
        query = ApplyKurumFilter(query, kurumId);

        if (tesisId.HasValue && tesisId.Value > 0)
        {
            query = query.Where(x => x.TesisId == tesisId.Value);
        }

        return await BuildDtoQuery(query).OrderBy(x => x.Ad).ToListAsync(cancellationToken);
    }

    public override async Task DeleteAsync(int id)
    {
        var cihaz = await _cihazRepository.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, q => q.Include(x => x.Terminaller)) ?? throw new BaseException("POS cihazı bulunamadı.", 404);
        EnforceKurum(cihaz.KurumId);
        foreach (var t in cihaz.Terminaller.Where(t => !t.IsDeleted))
        {
            t.AktifMi = false;
            t.IsDeleted = true;
        }

        cihaz.AktifMi = false;
        cihaz.IsDeleted = true;
        await _db.SaveChangesAsync();
    }

    public override async Task<PosCihaziDto?> GetByIdAsync(int id, Func<IQueryable<PosCihazi>, IQueryable<PosCihazi>>? include = null)
    {
        var cihaz = await _cihazRepository.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted) ?? throw new BaseException("POS cihazı bulunamadı.", 404);
        EnforceKurum(cihaz.KurumId);
        return await BuildDtoAsync(cihaz.Id);
    }

    private void EnforceKurum(int kurumId)
    {
        if (!_tenantAccessor.IsSuperAdmin() && !_tenantAccessor.GetAccessibleKurumIds().Contains(kurumId))
            throw new BaseException("Bu kuruma erişim yetkiniz yok.", 403);
    }

    private IQueryable<PosCihazi> ApplyKurumFilter(IQueryable<PosCihazi> query, int? kurumId)
    {
        if (kurumId.HasValue && kurumId.Value > 0)
        {
            EnforceKurum(kurumId.Value);
            return query.Where(x => x.KurumId == kurumId.Value);
        }

        if (_tenantAccessor.IsSuperAdmin())
        {
            return query;
        }

        var ids = _tenantAccessor.GetAccessibleKurumIds();
        return query.Where(x => ids.Contains(x.KurumId));
    }

    private async Task ValidateTesisAsync(int tesisId, int kurumId)
    {
        var tesis = await _db.Set<Tesis>().FirstOrDefaultAsync(x => x.Id == tesisId && !x.IsDeleted) ?? throw new BaseException("Tesis bulunamadı.", 400);
        if (tesis.KurumId != kurumId) throw new BaseException("Tesis seçilen kuruma ait değil.", 400);
    }

    private async Task ValidateAgentAsync(int agentId, int kurumId, int tesisId)
    {
        var a = await _db.Set<AgentEntity>().FirstOrDefaultAsync(x => x.Id == agentId && !x.IsDeleted) ?? throw new BaseException("Agent bulunamadı.", 400);
        if (a.KurumId != kurumId) throw new BaseException("Agent farklı bir kuruma ait.", 400);

        var tesisBaglantisiVarMi = await _db.Set<AgentTesis>().AnyAsync(x =>
            x.AgentId == agentId
            && x.KurumId == kurumId
            && x.TesisId == tesisId
            && x.AktifMi
            && !x.IsDeleted);

        if (!tesisBaglantisiVarMi)
        {
            throw new BaseException("Agent seçilen tesis kapsamında değil.", 400);
        }
    }

    private async Task<PosCihazi> GetCommandTargetAsync(int id, CancellationToken cancellationToken)
    {
        var cihaz = await _cihazRepository.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted) ?? throw new BaseException("POS cihazı bulunamadı.", 404);
        EnforceKurum(cihaz.KurumId);

        if (!cihaz.AktifMi)
        {
            throw new BaseException("POS cihazı pasif olduğu için komut gönderilemez.", 400);
        }

        if (!cihaz.AgentId.HasValue)
        {
            throw new BaseException("Bu POS cihazına atanmış agent bulunamadı.", 400);
        }

        var agent = await _db.Set<AgentEntity>().FirstOrDefaultAsync(x => x.Id == cihaz.AgentId.Value && !x.IsDeleted, cancellationToken)
            ?? throw new BaseException("POS cihazına bağlı agent bulunamadı.", 404);
        if (agent.KurumId != cihaz.KurumId)
        {
            throw new BaseException("POS cihazı ile agent aynı kurumda değil.", 400);
        }
        if (agent.Durum != AgentDurum.Active)
        {
            throw new BaseException("POS cihazına bağlı agent aktif değil.", 400);
        }

        var agentTesisBaglantisiVarMi = await _db.Set<AgentTesis>().AnyAsync(x =>
            x.AgentId == agent.Id
            && x.KurumId == cihaz.KurumId
            && x.TesisId == cihaz.TesisId
            && x.AktifMi
            && !x.IsDeleted, cancellationToken);

        if (!agentTesisBaglantisiVarMi)
        {
            throw new BaseException("Agent seçilen tesis kapsamında değil.", 400);
        }

        return cihaz;
    }

    private static void EnsurePavoCommandReady(PosCihazi cihaz)
    {
        if (cihaz.Saglayici != PosSaglayici.Pavo)
        {
            throw new BaseException("Bu cihaz PAVO sağlayıcısı olarak yapılandırılmamış.", 400);
        }

        if (string.IsNullOrWhiteSpace(cihaz.IpAdresi))
        {
            throw new BaseException("PAVO komutu için cihaz IP adresi zorunludur.", 400);
        }
    }

    private static PavoTransactionHandle BuildTransactionHandle(PosCihazi cihaz) => new()
    {
        SerialNumber = cihaz.SeriNo,
        Fingerprint = cihaz.Fingerprint ?? string.Empty,
        TransactionSequence = 0,
        TransactionDate = DateTime.UtcNow
    };

    private static PavoPairingRequest BuildPairingRequest(PosCihazi cihaz) => new()
    {
        PosCihaziId = cihaz.Id,
        IpAddress = cihaz.IpAdresi ?? string.Empty,
        HttpPort = cihaz.HttpPort,
        HttpsPort = cihaz.HttpsPort,
        UseHttps = cihaz.HttpsPort.HasValue,
        CurrentFingerprint = cihaz.Fingerprint,
        TransactionHandle = BuildTransactionHandle(cihaz)
    };

    private static PavoPingRequest BuildPingRequest(PosCihazi cihaz) => new()
    {
        PosCihaziId = cihaz.Id,
        IpAddress = cihaz.IpAdresi ?? string.Empty,
        HttpPort = cihaz.HttpPort,
        HttpsPort = cihaz.HttpsPort,
        UseHttps = cihaz.HttpsPort.HasValue,
        TransactionHandle = BuildTransactionHandle(cihaz)
    };

    private static PavoGetDeviceInfoRequest BuildGetDeviceInfoRequest(PosCihazi cihaz) => new()
    {
        PosCihaziId = cihaz.Id,
        IpAddress = cihaz.IpAdresi ?? string.Empty,
        HttpPort = cihaz.HttpPort,
        HttpsPort = cihaz.HttpsPort,
        UseHttps = cihaz.HttpsPort.HasValue,
        TransactionHandle = BuildTransactionHandle(cihaz)
    };

    private async Task<AgentCommandDto> SendCommandAsync(int agentId, string commandType, object payload, string requestedBy, CancellationToken cancellationToken)
    {
        return await _agentCommandService.SendAsync(new AgentCommandSendRequest
        {
            AgentId = agentId,
            CommandType = commandType,
            Payload = System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)),
            Priority = 1,
            ExpirationMinutes = 10,
            MaxRetryCount = 3
        }, requestedBy, cancellationToken);
    }

    private async Task ReconcileRegisteredTerminalsAsync(PosCihazi device, IReadOnlyCollection<PavoDeviceProvisioningCandidateTerminal> terminals, CancellationToken cancellationToken)
    {
        var existing = device.Terminaller.Where(x => !x.IsDeleted).ToList();
        var discovered = (terminals ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x.TerminalId))
            .Select(x => new
            {
                AcquirerId = NormalizeOptional(x.AcquirerId),
                AcquirerName = NormalizeOptional(x.AcquirerName),
                TerminalId = x.TerminalId.Trim(),
                MerchantId = NormalizeOptional(x.MerchantId),
                SourceReference = x.TerminalId.Trim()
            })
            .ToList();

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var terminal in discovered)
        {
            var canonicalKey = BuildTerminalCanonicalKey(device.Id, terminal.AcquirerId, terminal.TerminalId);
            if (!seen.Add(canonicalKey))
            {
                continue;
            }

            var current = existing.FirstOrDefault(x =>
                string.Equals(x.SaglayiciKodu, "PAVO", StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.CanonicalAcquirerId, terminal.AcquirerId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.CanonicalTerminalId, terminal.TerminalId, StringComparison.OrdinalIgnoreCase));

            if (current is null)
            {
                device.Terminaller.Add(new PosTerminal
                {
                    KurumId = device.KurumId,
                    TesisId = device.TesisId,
                    PosCihaziId = device.Id,
                    KasaBankaHesapId = null,
                    SaglayiciKodu = "PAVO",
                    AcquirerId = terminal.AcquirerId,
                    AcquirerName = terminal.AcquirerName,
                    CanonicalAcquirerId = NormalizeCanonicalValue(terminal.AcquirerId),
                    CanonicalTerminalId = terminal.TerminalId,
                    Ad = terminal.MerchantId ?? terminal.TerminalId,
                    SerialNumber = terminal.TerminalId,
                    SourceTerminalReference = terminal.MerchantId,
                    SourceFingerprint = null,
                    TargetFingerprint = null,
                    PairingId = null,
                    PairingCode = null,
                    EslesmeOnayliMi = !string.IsNullOrWhiteSpace(device.Fingerprint),
                    AktifMi = true,
                    CreatedBy = "agent",
                    CreatedAt = DateTime.UtcNow
                });
                continue;
            }

            current.KurumId = device.KurumId;
            current.TesisId = device.TesisId;
            current.PosCihaziId = device.Id;
            current.SaglayiciKodu = "PAVO";
            current.AcquirerId = terminal.AcquirerId;
            current.AcquirerName = terminal.AcquirerName;
            current.CanonicalAcquirerId = NormalizeCanonicalValue(terminal.AcquirerId);
            current.CanonicalTerminalId = terminal.TerminalId;
            current.Ad = terminal.MerchantId ?? current.Ad;
            current.SerialNumber = terminal.TerminalId;
            current.SourceTerminalReference = terminal.MerchantId ?? current.SourceTerminalReference;
            current.AktifMi = true;
            current.IsDeleted = false;
        }

        foreach (var terminal in existing.Where(x => !seen.Contains(BuildTerminalCanonicalKey(device.Id, x.CanonicalAcquirerId, x.CanonicalTerminalId))))
        {
            terminal.AktifMi = false;
        }
    }

    private async Task<PosCihaziDto> BuildDtoAsync(int id)
    {
        return await BuildDtoQuery(_db.PosCihazlari.AsNoTracking().Where(x => x.Id == id)).SingleAsync();
    }

    private IQueryable<PosCihaziDto> BuildDtoQuery(IQueryable<PosCihazi> baseQuery)
    {
        return from cihaz in baseQuery
               join tesis in _db.Tesisler.AsNoTracking() on cihaz.TesisId equals tesis.Id into tesisJoin
               from tesis in tesisJoin.DefaultIfEmpty()
               join agent in _db.Set<AgentEntity>().AsNoTracking() on cihaz.AgentId equals agent.Id into agentJoin
               from agent in agentJoin.DefaultIfEmpty()
               select new PosCihaziDto
               {
                   Id = cihaz.Id,
                   KurumId = cihaz.KurumId,
                   TesisId = cihaz.TesisId,
                   TesisAd = tesis != null ? tesis.Ad : null,
                   AgentId = cihaz.AgentId,
                   AgentAd = agent != null ? agent.Ad : null,
                   Saglayici = (int)cihaz.Saglayici,
                   Ad = cihaz.Ad,
                   SeriNo = cihaz.SeriNo,
                   IpAdresi = cihaz.IpAdresi,
                   AgentLocalDeviceId = cihaz.AgentLocalDeviceId,
                   HttpPort = cihaz.HttpPort,
                   HttpsPort = cihaz.HttpsPort,
                   Fingerprint = cihaz.Fingerprint,
                   TransactionSequence = cihaz.TransactionSequence,
                   EslesmeOnayliMi = cihaz.EslesmeOnayliMi,
                   AktifMi = cihaz.AktifMi,
                   SonBaglantiTarihi = cihaz.SonBaglantiTarihi,
                   Aciklama = cihaz.Aciklama,
                   TerminalSayisi = _db.PosTerminaller.Count(x => x.PosCihaziId == cihaz.Id && !x.IsDeleted)
               };
    }

    private static string BuildTerminalSourceReference(int deviceId, string? acquirerId, string terminalId) =>
        $"{deviceId}::{NormalizeOptional(acquirerId) ?? string.Empty}::{terminalId.Trim()}";

    private static string BuildTerminalCanonicalKey(int deviceId, string? acquirerId, string terminalId) =>
        $"{deviceId}:{NormalizeOptional(acquirerId)?.ToUpperInvariant() ?? string.Empty}:{terminalId.Trim()}";

    private static string NormalizeCanonicalValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();

    private static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
