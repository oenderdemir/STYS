using STYS.Muhasebe.TasinirKodMuhasebeHesapEslemeleri.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Muhasebe.TasinirKodMuhasebeHesapEslemeleri.Repositories;

public interface ITasinirKodMuhasebeHesapEslemeRepository : IBaseRdbmsRepository<TasinirKodMuhasebeHesapEsleme, int>
{
    Task<List<TasinirKodMuhasebeHesapEsleme>> GetByTasinirKodIdAsync(int tasinirKodId, CancellationToken cancellationToken = default);
    Task<TasinirKodMuhasebeHesapEsleme?> GetVarsayilanByIslemTuruAsync(int tasinirKodId, string islemTuru, CancellationToken cancellationToken = default);
    Task<TasinirKodMuhasebeHesapEsleme?> GetVarsayilanAsync(int tasinirKodId, string malzemeTipi, string hareketTipi, CancellationToken cancellationToken = default);
}
