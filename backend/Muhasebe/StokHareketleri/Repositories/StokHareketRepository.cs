using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.StokHareketleri.Dtos;
using STYS.Muhasebe.StokHareketleri.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Muhasebe.StokHareketleri.Repositories;

public class StokHareketRepository : BaseRdbmsRepository<StokHareket, int>, IStokHareketRepository
{
    private readonly StysAppDbContext _dbContext;

    public StokHareketRepository(StysAppDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
        _dbContext = dbContext;
    }

    public async Task<List<StokBakiyeDto>> GetDepoStokBakiyeleriAsync(int? depoId, CancellationToken cancellationToken = default)
    {
        var rows = await BuildBaseQuery(depoId)
            .Select(x => new
            {
                x.DepoId,
                DepoKod = x.Depo != null ? x.Depo.Kod : string.Empty,
                DepoAd = x.Depo != null ? x.Depo.Ad : string.Empty,
                x.TasinirKartId,
                StokKodu = x.TasinirKart != null ? x.TasinirKart.StokKodu : string.Empty,
                TasinirKartAd = x.TasinirKart != null ? x.TasinirKart.Ad : string.Empty,
                Birim = x.TasinirKart != null ? x.TasinirKart.Birim : string.Empty,
                Giris = StokHareketTipleri.GirisEtkisi.Contains(x.HareketTipi) ? x.Miktar : 0m,
                Cikis = StokHareketTipleri.CikisEtkisi.Contains(x.HareketTipi) ? x.Miktar : 0m
            })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => new { x.DepoId, x.DepoKod, x.DepoAd, x.TasinirKartId, x.StokKodu, x.TasinirKartAd, x.Birim })
            .Select(g => new StokBakiyeDto
            {
                DepoId = g.Key.DepoId,
                DepoKod = g.Key.DepoKod,
                DepoAd = g.Key.DepoAd,
                TasinirKartId = g.Key.TasinirKartId,
                StokKodu = g.Key.StokKodu,
                TasinirKartAd = g.Key.TasinirKartAd,
                Birim = g.Key.Birim,
                GirisMiktari = g.Sum(x => x.Giris),
                CikisMiktari = g.Sum(x => x.Cikis),
                BakiyeMiktari = g.Sum(x => x.Giris) - g.Sum(x => x.Cikis)
            })
            .OrderBy(x => x.DepoKod)
            .ThenBy(x => x.StokKodu)
            .ToList();
    }

    public async Task<List<StokKartOzetDto>> GetStokKartOzetleriAsync(int? depoId, CancellationToken cancellationToken = default)
    {
        var rows = await BuildBaseQuery(depoId)
            .Select(x => new
            {
                x.TasinirKartId,
                StokKodu = x.TasinirKart != null ? x.TasinirKart.StokKodu : string.Empty,
                Ad = x.TasinirKart != null ? x.TasinirKart.Ad : string.Empty,
                Birim = x.TasinirKart != null ? x.TasinirKart.Birim : string.Empty,
                Giris = StokHareketTipleri.GirisEtkisi.Contains(x.HareketTipi) ? x.Miktar : 0m,
                Cikis = StokHareketTipleri.CikisEtkisi.Contains(x.HareketTipi) ? x.Miktar : 0m
            })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => new { x.TasinirKartId, x.StokKodu, x.Ad, x.Birim })
            .Select(g => new StokKartOzetDto
            {
                TasinirKartId = g.Key.TasinirKartId,
                StokKodu = g.Key.StokKodu,
                Ad = g.Key.Ad,
                Birim = g.Key.Birim,
                GirisMiktari = g.Sum(x => x.Giris),
                CikisMiktari = g.Sum(x => x.Cikis),
                BakiyeMiktari = g.Sum(x => x.Giris) - g.Sum(x => x.Cikis)
            })
            .OrderBy(x => x.StokKodu)
            .ToList();
    }

    private IQueryable<StokHareket> BuildBaseQuery(int? depoId)
    {
        var query = _dbContext.StokHareketleri
            .AsNoTracking()
            .Include(x => x.Depo)
            .Include(x => x.TasinirKart)
            .Where(x => x.Durum == StokHareketDurumlari.Aktif);

        if (depoId.HasValue && depoId.Value > 0)
        {
            query = query.Where(x => x.DepoId == depoId.Value);
        }

        return query;
    }
}
