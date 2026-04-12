using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.MusteriMenu.Dtos;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.MusteriMenu.Services;

public class MusteriMenuService : IMusteriMenuService
{
    private readonly StysAppDbContext _dbContext;

    public MusteriMenuService(StysAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<MusteriMenuDto> GetByRestoranIdAsync(int restoranId, CancellationToken cancellationToken = default)
    {
        if (restoranId <= 0)
        {
            throw new BaseException("Gecerli restoran secimi zorunludur.", 400);
        }

        var restoran = await _dbContext.Restoranlar
            .AsNoTracking()
            .Where(x => x.Id == restoranId && x.AktifMi)
            .Select(x => new MusteriRestoranOzetDto
            {
                Id = x.Id,
                Ad = x.Ad,
                Aciklama = x.Aciklama
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (restoran is null)
        {
            throw new BaseException("Restoran bulunamadi.", 404);
        }

        var kategoriler = await _dbContext.RestoranMenuKategorileri
            .AsNoTracking()
            .Where(x => x.RestoranId == restoranId && x.AktifMi)
            .OrderBy(x => x.SiraNo)
            .ThenBy(x => x.Ad)
            .ThenBy(x => x.Id)
            .Select(x => new MusteriMenuKategoriDto
            {
                Id = x.Id,
                Ad = x.Ad,
                SiraNo = x.SiraNo,
                Urunler = x.Urunler
                    .Where(u => u.AktifMi)
                    .OrderBy(u => u.Ad)
                    .ThenBy(u => u.Id)
                    .Select(u => new MusteriMenuUrunDto
                    {
                        Id = u.Id,
                        KategoriId = x.Id,
                        Ad = u.Ad,
                        Aciklama = u.Aciklama,
                        Fiyat = u.Fiyat,
                        ParaBirimi = u.ParaBirimi,
                        HazirlamaSuresiDakika = u.HazirlamaSuresiDakika
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        return new MusteriMenuDto
        {
            Restoran = restoran,
            Kategoriler = kategoriler
        };
    }
}
