using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.TasinirKodMuhasebeHesapEslemeleri.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Muhasebe.TasinirKodMuhasebeHesapEslemeleri.Repositories;

public class TasinirKodMuhasebeHesapEslemeRepository
    : BaseRdbmsRepository<TasinirKodMuhasebeHesapEsleme, int>,
      ITasinirKodMuhasebeHesapEslemeRepository
{
    private readonly StysAppDbContext _dbContext;

    public TasinirKodMuhasebeHesapEslemeRepository(StysAppDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
        _dbContext = dbContext;
    }

    public async Task<List<TasinirKodMuhasebeHesapEsleme>> GetByTasinirKodIdAsync(int tasinirKodId, CancellationToken cancellationToken = default)
        => await _dbContext.TasinirKodMuhasebeHesapEslemeleri
            .Include(x => x.TasinirKod)
            .Include(x => x.MuhasebeHesapPlani)
            .Where(x => x.TasinirKodId == tasinirKodId)
            .OrderBy(x => x.IslemTuru)
            .ToListAsync(cancellationToken);

    public async Task<TasinirKodMuhasebeHesapEsleme?> GetVarsayilanByIslemTuruAsync(int tasinirKodId, string islemTuru, CancellationToken cancellationToken = default)
        => await _dbContext.TasinirKodMuhasebeHesapEslemeleri
            .Include(x => x.MuhasebeHesapPlani)
            .FirstOrDefaultAsync(x => x.TasinirKodId == tasinirKodId
                                   && x.IslemTuru == islemTuru
                                   && x.VarsayilanMi
                                   && x.AktifMi, cancellationToken);

    public async Task<TasinirKodMuhasebeHesapEsleme?> GetVarsayilanAsync(int tasinirKodId, string malzemeTipi, string hareketTipi, CancellationToken cancellationToken = default)
        => await _dbContext.TasinirKodMuhasebeHesapEslemeleri
            .Include(x => x.MuhasebeHesapPlani)
            .FirstOrDefaultAsync(x => x.TasinirKodId == tasinirKodId
                                   && x.MalzemeTipi == malzemeTipi
                                   && x.HareketTipi == hareketTipi
                                   && x.VarsayilanMi
                                   && x.AktifMi, cancellationToken);
}
