using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.Agent.Entities;
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

    public PosCihaziService(IPosCihaziRepository repository, IMapper mapper, ICurrentTenantAccessor tenantAccessor, StysAppDbContext db)
        : base(repository, mapper)
    {
        _tenantAccessor = tenantAccessor;
        _db = db;
        _cihazRepository = repository;
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
                   EslesmeOnayliMi = cihaz.EslesmeOnayliMi,
                   AktifMi = cihaz.AktifMi,
                   SonBaglantiTarihi = cihaz.SonBaglantiTarihi,
                   Aciklama = cihaz.Aciklama,
                   TerminalSayisi = _db.PosTerminaller.Count(x => x.PosCihaziId == cihaz.Id && !x.IsDeleted)
               };
    }
}
