using AutoMapper;
using System.Data;
using Microsoft.EntityFrameworkCore;
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
    private readonly StysAppDbContext _db;
    private readonly IPosCihaziRepository _cihazRepository;
    private readonly AgentCommandService _agentCommandService;

    public PosCihaziService(
        IPosCihaziRepository repository,
        IMapper mapper,
        ICurrentTenantAccessor tenantAccessor,
        StysAppDbContext db,
        AgentCommandService agentCommandService)
        : base(repository, mapper)
    {
        _tenantAccessor = tenantAccessor;
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
        var sequence = await ReserveTransactionSequenceAsync(device.Id, cancellationToken);
        var request = BuildPairingRequest(device, sequence);
        return await SendCommandAsync(device.AgentId!.Value, "PavoPairing", request, requestedBy, cancellationToken);
    }

    public async Task<AgentCommandDto> PingAsync(int id, string requestedBy, CancellationToken cancellationToken)
    {
        var device = await GetCommandTargetAsync(id, cancellationToken);
        EnsurePavoCommandReady(device);
        var sequence = await ReserveTransactionSequenceAsync(device.Id, cancellationToken);
        var request = BuildPingRequest(device, sequence);
        return await SendCommandAsync(device.AgentId!.Value, "PavoPing", request, requestedBy, cancellationToken);
    }

    public async Task<AgentCommandDto> GetDeviceInfoAsync(int id, string requestedBy, CancellationToken cancellationToken)
    {
        var device = await GetCommandTargetAsync(id, cancellationToken);
        EnsurePavoCommandReady(device);
        var sequence = await ReserveTransactionSequenceAsync(device.Id, cancellationToken);
        var request = BuildGetDeviceInfoRequest(device, sequence);
        return await SendCommandAsync(device.AgentId!.Value, "PavoGetDeviceInfo", request, requestedBy, cancellationToken);
    }

    public override async Task<IEnumerable<PosCihaziDto>> GetAllAsync(Func<IQueryable<PosCihazi>, IQueryable<PosCihazi>>? include = null)
    {
        var query = BuildDtoQuery(_db.PosCihazlari.AsNoTracking().Where(x => !x.IsDeleted));
        return await query.OrderBy(x => x.Ad).ToListAsync();
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

    private async Task<long> ReserveTransactionSequenceAsync(int cihazId, CancellationToken cancellationToken)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var cihaz = await _db.PosCihazlari.FirstAsync(x => x.Id == cihazId && !x.IsDeleted, cancellationToken);
        cihaz.TransactionSequence++;
        var sequence = cihaz.TransactionSequence;
        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return sequence;
    }

    private static PavoTransactionHandle BuildTransactionHandle(PosCihazi cihaz, long sequence) => new()
    {
        SerialNumber = cihaz.SeriNo,
        Fingerprint = cihaz.Fingerprint ?? string.Empty,
        TransactionSequence = sequence,
        TransactionDate = DateTime.UtcNow
    };

    private static PavoPairingRequest BuildPairingRequest(PosCihazi cihaz, long sequence) => new()
    {
        PosCihaziId = cihaz.Id,
        KurumId = cihaz.KurumId,
        TesisId = cihaz.TesisId,
        IpAddress = cihaz.IpAdresi ?? string.Empty,
        HttpPort = cihaz.HttpPort,
        HttpsPort = cihaz.HttpsPort,
        UseHttps = cihaz.HttpsPort.HasValue,
        CurrentFingerprint = cihaz.Fingerprint,
        TransactionHandle = BuildTransactionHandle(cihaz, sequence)
    };

    private static PavoPingRequest BuildPingRequest(PosCihazi cihaz, long sequence) => new()
    {
        PosCihaziId = cihaz.Id,
        KurumId = cihaz.KurumId,
        TesisId = cihaz.TesisId,
        IpAddress = cihaz.IpAdresi ?? string.Empty,
        HttpPort = cihaz.HttpPort,
        HttpsPort = cihaz.HttpsPort,
        UseHttps = cihaz.HttpsPort.HasValue,
        TransactionHandle = BuildTransactionHandle(cihaz, sequence)
    };

    private static PavoGetDeviceInfoRequest BuildGetDeviceInfoRequest(PosCihazi cihaz, long sequence) => new()
    {
        PosCihaziId = cihaz.Id,
        KurumId = cihaz.KurumId,
        TesisId = cihaz.TesisId,
        IpAddress = cihaz.IpAdresi ?? string.Empty,
        HttpPort = cihaz.HttpPort,
        HttpsPort = cihaz.HttpsPort,
        UseHttps = cihaz.HttpsPort.HasValue,
        TransactionHandle = BuildTransactionHandle(cihaz, sequence)
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
}
