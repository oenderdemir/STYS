using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.Entegrasyonlar.Pos.Dtos;
using STYS.Entegrasyonlar.Pos.Entities;
using STYS.Entegrasyonlar.Pos.Repositories;
using STYS.Infrastructure.EntityFramework;
using STYS.Tesisler.Entities;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;

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
        dto.KurumId = kurumId;
        await ValidateTesisAsync(dto.TesisId, kurumId);
        if (dto.AgentId.HasValue) await ValidateAgentKurumAsync(dto.AgentId.Value, kurumId);
        var duplicate = await _cihazRepository.AnyAsync(x => x.SeriNo == dto.SeriNo && x.KurumId == kurumId && !x.IsDeleted);
        if (duplicate) throw new BaseException("Bu seri numarasına sahip cihaz zaten kayıtlı.", 400);
        return await base.AddAsync(dto);
    }

    public override async Task<PosCihaziDto> UpdateAsync(PosCihaziDto dto)
    {
        var existing = await _cihazRepository.FirstOrDefaultAsync(x => x.Id == dto.Id && !x.IsDeleted) ?? throw new BaseException("POS cihazı bulunamadı.", 404);
        EnforceKurum(existing.KurumId);
        if (dto.AgentId.HasValue) await ValidateAgentKurumAsync(dto.AgentId.Value, existing.KurumId);
        return await base.UpdateAsync(dto);
    }

    public override async Task DeleteAsync(int id)
    {
        var cihaz = await _cihazRepository.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, q => q.Include(x => x.Terminaller)) ?? throw new BaseException("POS cihazı bulunamadı.", 404);
        EnforceKurum(cihaz.KurumId);
        foreach (var t in cihaz.Terminaller.Where(t => !t.IsDeleted)) { t.AktifMi = false; t.IsDeleted = true; }
        await base.DeleteAsync(id);
    }

    public override async Task<PosCihaziDto?> GetByIdAsync(int id, Func<IQueryable<PosCihazi>, IQueryable<PosCihazi>>? include = null)
    {
        var cihaz = await _cihazRepository.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted) ?? throw new BaseException("POS cihazı bulunamadı.", 404);
        EnforceKurum(cihaz.KurumId);
        return await base.GetByIdAsync(id, include);
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

    private async Task ValidateAgentKurumAsync(int agentId, int kurumId)
    {
        var a = await _db.Set<Agent.Entities.Agent>().FirstOrDefaultAsync(x => x.Id == agentId && !x.IsDeleted) ?? throw new BaseException("Agent bulunamadı.", 400);
        if (a.KurumId != kurumId) throw new BaseException("Agent farklı bir kuruma ait.", 400);
    }
}
