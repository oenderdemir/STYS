using Microsoft.EntityFrameworkCore;
using STYS.Entegrasyonlar.Pos.Dtos;
using STYS.Entegrasyonlar.Pos.Entities;
using STYS.Infrastructure.EntityFramework;
using STYS.Tesisler.Entities;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Entegrasyonlar.Pos.Services;

public sealed class PosCihaziService
{
    private readonly IDbContextFactory<StysAppDbContext> _dbFactory;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public PosCihaziService(IDbContextFactory<StysAppDbContext> dbFactory, ICurrentTenantAccessor tenantAccessor)
    {
        _dbFactory = dbFactory;
        _tenantAccessor = tenantAccessor;
    }

    public async Task<List<PosCihaziDto>> GetAllAsync(int? tesisId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var query = db.PosCihazlari.Where(x => !x.IsDeleted);
        if (!_tenantAccessor.IsSuperAdmin())
            query = query.Where(x => _tenantAccessor.GetAccessibleKurumIds().Contains(x.KurumId));
        if (tesisId.HasValue)
            query = query.Where(x => x.TesisId == tesisId.Value);

        return await query.Select(x => new PosCihaziDto
        {
            Id = x.Id, KurumId = x.KurumId, TesisId = x.TesisId, AgentId = x.AgentId,
            Saglayici = (int)x.Saglayici, Ad = x.Ad, SeriNo = x.SeriNo,
            IpAdresi = x.IpAdresi, HttpPort = x.HttpPort, HttpsPort = x.HttpsPort,
            Fingerprint = x.Fingerprint, EslesmeOnayliMi = x.EslesmeOnayliMi,
            AktifMi = x.AktifMi, SonBaglantiTarihi = x.SonBaglantiTarihi, Aciklama = x.Aciklama,
            TerminalSayisi = x.Terminaller.Count(t => !t.IsDeleted)
        }).ToListAsync(ct);
    }

    public async Task<PosCihaziDto> GetByIdAsync(int id, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var x = await db.PosCihazlari.FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct)
            ?? throw new BaseException("POS cihazı bulunamadı.", 404);
        EnforceKurum(x.KurumId);
        return MapDto(x);
    }

    public async Task<PosCihaziDto> CreateAsync(PosCihaziKaydetRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Ad)) throw new BaseException("Ad gereklidir.", 400);
        if (string.IsNullOrWhiteSpace(req.SeriNo)) throw new BaseException("Seri no gereklidir.", 400);

        var kurumId = _tenantAccessor.GetCurrentKurumId();
        if (!kurumId.HasValue) throw new BaseException("Aktif kurum seçilmedi.", 400);
        EnforceKurum(kurumId.Value);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        await ValidateTesisAsync(db, req.TesisId, kurumId.Value, ct);
        if (req.AgentId.HasValue)
            await ValidateAgentKurumAsync(db, req.AgentId.Value, kurumId.Value, ct);

        var exists = await db.PosCihazlari.AnyAsync(x => x.SeriNo == req.SeriNo && x.KurumId == kurumId.Value && !x.IsDeleted, ct);
        if (exists) throw new BaseException("Bu seri numarasına sahip cihaz zaten kayıtlı.", 400);

        var cihaz = new PosCihazi
        {
            KurumId = kurumId.Value, TesisId = req.TesisId, AgentId = req.AgentId,
            Saglayici = (PosSaglayici)req.Saglayici, Ad = req.Ad, SeriNo = req.SeriNo,
            IpAdresi = req.IpAdresi, HttpPort = req.HttpPort, HttpsPort = req.HttpsPort,
            Fingerprint = req.Fingerprint, Aciklama = req.Aciklama,
            AktifMi = true, CreatedBy = "system", CreatedAt = DateTime.UtcNow
        };
        db.PosCihazlari.Add(cihaz);
        await db.SaveChangesAsync(ct);
        return MapDto(cihaz);
    }

    public async Task<PosCihaziDto> UpdateAsync(int id, PosCihaziKaydetRequest req, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var cihaz = await db.PosCihazlari.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct)
            ?? throw new BaseException("POS cihazı bulunamadı.", 404);
        EnforceKurum(cihaz.KurumId);

        if (req.AgentId.HasValue)
            await ValidateAgentKurumAsync(db, req.AgentId.Value, cihaz.KurumId, ct);

        cihaz.Ad = req.Ad; cihaz.SeriNo = req.SeriNo; cihaz.IpAdresi = req.IpAdresi;
        cihaz.HttpPort = req.HttpPort; cihaz.HttpsPort = req.HttpsPort;
        cihaz.Fingerprint = req.Fingerprint; cihaz.Aciklama = req.Aciklama;
        cihaz.AgentId = req.AgentId; cihaz.TesisId = req.TesisId;
        await db.SaveChangesAsync(ct);
        return MapDto(cihaz);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var cihaz = await db.PosCihazlari.Include(x => x.Terminaller).FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct)
            ?? throw new BaseException("POS cihazı bulunamadı.", 404);
        EnforceKurum(cihaz.KurumId);
        cihaz.AktifMi = false; cihaz.IsDeleted = true;
        foreach (var t in cihaz.Terminaller) { t.AktifMi = false; t.IsDeleted = true; }
        await db.SaveChangesAsync(ct);
    }

    private void EnforceKurum(int kurumId)
    {
        if (!_tenantAccessor.IsSuperAdmin() && !_tenantAccessor.GetAccessibleKurumIds().Contains(kurumId))
            throw new BaseException("Bu kuruma erişim yetkiniz yok.", 403);
    }

    private static async Task ValidateTesisAsync(StysAppDbContext db, int tesisId, int kurumId, CancellationToken ct)
    {
        var tesis = await db.Set<Tesis>().FirstOrDefaultAsync(x => x.Id == tesisId && !x.IsDeleted, ct)
            ?? throw new BaseException("Tesis bulunamadı.", 400);
        if (tesis.KurumId != kurumId)
            throw new BaseException("Tesis seçilen kuruma ait değil.", 400);
    }

    private static async Task ValidateAgentKurumAsync(StysAppDbContext db, int agentId, int kurumId, CancellationToken ct)
    {
        var agent = await db.Set<STYS.Agent.Entities.Agent>().FirstOrDefaultAsync(x => x.Id == agentId && !x.IsDeleted, ct)
            ?? throw new BaseException("Agent bulunamadı.", 400);
        if (agent.KurumId != kurumId)
            throw new BaseException("Agent farklı bir kuruma ait.", 400);
    }

    private static PosCihaziDto MapDto(PosCihazi x) => new()
    {
        Id = x.Id, KurumId = x.KurumId, TesisId = x.TesisId, AgentId = x.AgentId,
        Saglayici = (int)x.Saglayici, Ad = x.Ad, SeriNo = x.SeriNo,
        IpAdresi = x.IpAdresi, HttpPort = x.HttpPort, HttpsPort = x.HttpsPort,
        Fingerprint = x.Fingerprint, EslesmeOnayliMi = x.EslesmeOnayliMi,
        AktifMi = x.AktifMi, SonBaglantiTarihi = x.SonBaglantiTarihi, Aciklama = x.Aciklama,
        TerminalSayisi = x.Terminaller?.Count(t => !t.IsDeleted) ?? 0
    };
}
