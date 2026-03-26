using STYS.EkHizmetler.Dto;
using STYS.EkHizmetler.Entities;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.EkHizmetler.Services;

public interface IEkHizmetTarifeService : IBaseRdbmsService<EkHizmetTarifeDto, EkHizmetTarife, int>
{
    Task<List<EkHizmetTesisDto>> GetErisilebilirTesislerAsync(CancellationToken cancellationToken = default);

    Task<List<EkHizmetDto>> GetHizmetlerByTesisIdAsync(int tesisId, CancellationToken cancellationToken = default);

    Task<List<EkHizmetDto>> UpsertHizmetlerByTesisAsync(int tesisId, IEnumerable<EkHizmetDto> hizmetler, CancellationToken cancellationToken = default);

    Task<List<EkHizmetTarifeDto>> GetByTesisIdAsync(int tesisId, CancellationToken cancellationToken = default);

    Task<List<EkHizmetTarifeDto>> UpsertByTesisAsync(int tesisId, IEnumerable<EkHizmetTarifeDto> tarifeler, CancellationToken cancellationToken = default);
}
