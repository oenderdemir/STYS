using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.TevkifatHesapEslemeleri.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Muhasebe.TevkifatHesapEslemeleri.Repositories;

public class TevkifatHesapEslemeRepository
    : BaseRdbmsRepository<TevkifatHesapEsleme, int>,
      ITevkifatHesapEslemeRepository
{
    private readonly StysAppDbContext _dbContext;

    public TevkifatHesapEslemeRepository(StysAppDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
        _dbContext = dbContext;
    }

    public async Task<TevkifatHesapEsleme?> GetAktifEslemeAsync(
        int? tesisId,
        string islemYonu,
        int tevkifatPay,
        int tevkifatPayda,
        CancellationToken cancellationToken = default)
    {
        if (tesisId.HasValue)
        {
            var tesisOzel = await _dbContext.TevkifatHesapEslemeleri
                .Include(x => x.MuhasebeHesapPlani)
                .Include(x => x.Tesis)
                .FirstOrDefaultAsync(x =>
                    x.TesisId == tesisId.Value
                    && x.IslemYonu == islemYonu
                    && x.TevkifatPay == tevkifatPay
                    && x.TevkifatPayda == tevkifatPayda
                    && x.AktifMi
                    && !x.IsDeleted,
                    cancellationToken);

            if (tesisOzel is not null)
            {
                return tesisOzel;
            }
        }

        return await _dbContext.TevkifatHesapEslemeleri
            .Include(x => x.MuhasebeHesapPlani)
            .Include(x => x.Tesis)
            .FirstOrDefaultAsync(x =>
                x.TesisId == null
                && x.IslemYonu == islemYonu
                && x.TevkifatPay == tevkifatPay
                && x.TevkifatPayda == tevkifatPayda
                && x.AktifMi
                && !x.IsDeleted,
                cancellationToken);
    }
}
