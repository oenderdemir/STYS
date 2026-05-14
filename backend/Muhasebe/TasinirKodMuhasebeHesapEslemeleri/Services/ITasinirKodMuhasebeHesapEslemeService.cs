using STYS.Muhasebe.TasinirKodMuhasebeHesapEslemeleri.Dtos;
using STYS.Muhasebe.TasinirKodMuhasebeHesapEslemeleri.Entities;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.Muhasebe.TasinirKodMuhasebeHesapEslemeleri.Services;

public interface ITasinirKodMuhasebeHesapEslemeService : IBaseRdbmsService<TasinirKodMuhasebeHesapEslemeDto, TasinirKodMuhasebeHesapEsleme, int>
{
    Task<List<TasinirKodMuhasebeHesapEslemeDto>> GetByTasinirKodIdAsync(int tasinirKodId, CancellationToken cancellationToken = default);
    Task<TasinirKodMuhasebeHesapEslemeDto?> GetVarsayilanAsync(int tasinirKodId, string malzemeTipi, string hareketTipi, CancellationToken cancellationToken = default);
}
