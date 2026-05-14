using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.MuhasebeVergiHesapEslemeleri.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Muhasebe.MuhasebeVergiHesapEslemeleri.Repositories;

public class MuhasebeVergiHesapEslemeRepository
    : BaseRdbmsRepository<MuhasebeVergiHesapEsleme, int>,
      IMuhasebeVergiHesapEslemeRepository
{
    private readonly StysAppDbContext _dbContext;

    public MuhasebeVergiHesapEslemeRepository(StysAppDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
        _dbContext = dbContext;
    }

    public async Task<MuhasebeVergiHesapEsleme?> GetAktifEslemeAsync(
        string vergiTipi,
        decimal oran,
        int? tesisId,
        CancellationToken cancellationToken = default)
    {
        // Önce tesis özel kayıt ara
        if (tesisId.HasValue)
        {
            var tesisOzel = await _dbContext.MuhasebeVergiHesapEslemeleri
                .Include(x => x.AlisKdvHesap)
                .Include(x => x.SatisKdvHesap)
                .FirstOrDefaultAsync(x =>
                    x.TesisId == tesisId.Value
                    && x.VergiTipi == vergiTipi
                    && x.Oran == oran
                    && x.AktifMi
                    && !x.IsDeleted,
                    cancellationToken);

            if (tesisOzel is not null)
                return tesisOzel;
        }

        // Tesis özel bulunamazsa genel kayıt ara
        return await _dbContext.MuhasebeVergiHesapEslemeleri
            .Include(x => x.AlisKdvHesap)
            .Include(x => x.SatisKdvHesap)
            .FirstOrDefaultAsync(x =>
                x.TesisId == null
                && x.VergiTipi == vergiTipi
                && x.Oran == oran
                && x.AktifMi
                && !x.IsDeleted,
                cancellationToken);
    }
}
