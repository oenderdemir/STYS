using STYS.EkHizmetler.Dto;
using STYS.EkHizmetler.Entities;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.EkHizmetler.Services;

public interface IEkHizmetTarifeService : IBaseRdbmsService<EkHizmetTarifeDto, EkHizmetTarife, int>
{
    Task<List<EkHizmetTesisDto>> GetErisilebilirTesislerAsync(CancellationToken cancellationToken = default);

    Task<List<GlobalEkHizmetTanimiDto>> GetGlobalTanimlarAsync(CancellationToken cancellationToken = default);

    Task<GlobalEkHizmetTanimiDto?> GetGlobalTanimByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<GlobalEkHizmetTanimiDto> AddGlobalTanimAsync(GlobalEkHizmetTanimiDto dto, CancellationToken cancellationToken = default);

    Task<GlobalEkHizmetTanimiDto> UpdateGlobalTanimAsync(int id, GlobalEkHizmetTanimiDto dto, CancellationToken cancellationToken = default);

    Task DeleteGlobalTanimAsync(int id, CancellationToken cancellationToken = default);

    Task<List<EkHizmetTesisAtamaDto>> GetTesisAtamalariAsync(int tesisId, CancellationToken cancellationToken = default);

    Task<List<EkHizmetTesisAtamaDto>> KaydetTesisAtamalariAsync(int tesisId, IReadOnlyCollection<int> globalTanimIds, CancellationToken cancellationToken = default);

    Task<List<EkHizmetDto>> GetHizmetlerByTesisIdAsync(int tesisId, CancellationToken cancellationToken = default);

    Task<List<EkHizmetTarifeDto>> GetByTesisIdAsync(int tesisId, CancellationToken cancellationToken = default);

    Task<List<EkHizmetTarifeDto>> UpsertByTesisAsync(int tesisId, IEnumerable<EkHizmetTarifeDto> tarifeler, CancellationToken cancellationToken = default);
}
