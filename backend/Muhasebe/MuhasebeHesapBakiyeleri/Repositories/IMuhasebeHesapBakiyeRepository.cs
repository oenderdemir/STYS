using STYS.Muhasebe.MuhasebeHesapBakiyeleri.Dtos;
using STYS.Muhasebe.MuhasebeHesapBakiyeleri.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Muhasebe.MuhasebeHesapBakiyeleri.Repositories;

public interface IMuhasebeHesapBakiyeRepository : IBaseRdbmsRepository<MuhasebeHesapBakiye, int>
{
    Task<MuhasebeHesapBakiye?> GetByUniqueKeyAsync(
        int tesisId,
        int maliYil,
        int donem,
        int muhasebeHesapPlaniId,
        bool konsolideMi,
        CancellationToken cancellationToken = default);

    Task<List<MuhasebeHesapBakiye>> GetFilteredAsync(
        MuhasebeHesapBakiyeFilterDto filter,
        CancellationToken cancellationToken = default);

    Task<int> CountFilteredAsync(
        MuhasebeHesapBakiyeFilterDto filter,
        CancellationToken cancellationToken = default);

    Task<List<MuhasebeHesapBakiye>> GetByTesisYilDonemAsync(
        int tesisId,
        int maliYil,
        int donem,
        CancellationToken cancellationToken = default);
}
