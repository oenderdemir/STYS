using STYS.Muhasebe.MuhasebeHesapBakiyeleri.Dtos;
using STYS.Muhasebe.MuhasebeHesapBakiyeleri.Entities;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.Muhasebe.MuhasebeHesapBakiyeleri.Services;

public interface IMuhasebeHesapBakiyeService : IBaseRdbmsService<MuhasebeHesapBakiyeDto, MuhasebeHesapBakiye, int>
{
    Task<List<MuhasebeHesapBakiyeDto>> GetFilteredAsync(
        MuhasebeHesapBakiyeFilterDto filter,
        CancellationToken cancellationToken = default);

    Task<int> CountFilteredAsync(
        MuhasebeHesapBakiyeFilterDto filter,
        CancellationToken cancellationToken = default);

    Task<List<MuhasebeHesapBakiyeDto>> GetByTesisYilDonemAsync(
        int tesisId,
        int maliYil,
        int donem,
        CancellationToken cancellationToken = default);
}
