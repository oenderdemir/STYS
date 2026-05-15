using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.MuhasebeDonemleri.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Muhasebe.MuhasebeDonemleri.Repositories;

public class MuhasebeDonemRepository
    : BaseRdbmsRepository<MuhasebeDonem, int>,
      IMuhasebeDonemRepository
{
    private readonly StysAppDbContext _dbContext;

    public MuhasebeDonemRepository(StysAppDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
        _dbContext = dbContext;
    }

    public async Task<MuhasebeDonem?> GetAktifDonemAsync(
        int tesisId,
        DateTime tarih,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.MuhasebeDonemler
            .Include(x => x.Tesis)
            .FirstOrDefaultAsync(x =>
                x.TesisId == tesisId
                && !x.IsDeleted
                && !x.KapaliMi
                && x.BaslangicTarihi <= tarih
                && x.BitisTarihi >= tarih,
                cancellationToken);
    }

    public async Task<MuhasebeDonem?> GetByTesisYilDonemAsync(
        int tesisId,
        int maliYil,
        int donemNo,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.MuhasebeDonemler
            .Include(x => x.Tesis)
            .FirstOrDefaultAsync(x =>
                x.TesisId == tesisId
                && x.MaliYil == maliYil
                && x.DonemNo == donemNo
                && !x.IsDeleted,
                cancellationToken);
    }

    public async Task<bool> TarihAraligiCakisiyorMuAsync(
        int tesisId,
        DateTime baslangic,
        DateTime bitis,
        int? haricId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.MuhasebeDonemler
            .Where(x =>
                x.TesisId == tesisId
                && !x.IsDeleted
                && x.BaslangicTarihi <= bitis
                && baslangic <= x.BitisTarihi);

        if (haricId.HasValue)
            query = query.Where(x => x.Id != haricId.Value);

        return await query.AnyAsync(cancellationToken);
    }
}
