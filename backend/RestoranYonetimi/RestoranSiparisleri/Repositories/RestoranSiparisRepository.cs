using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.RestoranSiparisleri.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.RestoranSiparisleri.Repositories;

public class RestoranSiparisRepository : BaseRdbmsRepository<RestoranSiparis, int>, IRestoranSiparisRepository
{
    private readonly StysAppDbContext _dbContext;

    public RestoranSiparisRepository(StysAppDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
        _dbContext = dbContext;
    }

    public Task<RestoranSiparis?> GetDetayByIdAsync(int id, CancellationToken cancellationToken = default)
        => _dbContext.RestoranSiparisleri
            .Include(x => x.Kalemler)
            .Include(x => x.Odemeler)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<List<RestoranSiparis>> GetByRestoranIdAsync(int restoranId, CancellationToken cancellationToken = default)
        => _dbContext.RestoranSiparisleri
            .Include(x => x.Kalemler)
            .Include(x => x.Odemeler)
            .Where(x => x.RestoranId == restoranId)
            .OrderByDescending(x => x.SiparisTarihi)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

    public Task<List<RestoranSiparis>> GetAcikSiparislerAsync(int? masaId, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.RestoranSiparisleri
            .Include(x => x.Kalemler)
            .Include(x => x.Odemeler)
            .Where(x => RestoranSiparisDurumlari.AcikSiparisDurumlari.Contains(x.SiparisDurumu));

        if (masaId.HasValue && masaId.Value > 0)
        {
            query = query.Where(x => x.RestoranMasaId == masaId.Value);
        }

        return query
            .OrderByDescending(x => x.SiparisTarihi)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    public Task<RestoranSiparis?> GetMasaAcikSiparisAsync(int masaId, CancellationToken cancellationToken = default)
        => _dbContext.RestoranSiparisleri
            .Include(x => x.Kalemler)
            .Include(x => x.Odemeler)
            .FirstOrDefaultAsync(
                x => x.RestoranMasaId == masaId && RestoranSiparisDurumlari.AcikSiparisDurumlari.Contains(x.SiparisDurumu),
                cancellationToken);
}
