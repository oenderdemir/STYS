using STYS.MisafirTipleri.Dto;
using STYS.MisafirTipleri.Entities;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.MisafirTipleri.Services;

public interface IMisafirTipiService : IBaseRdbmsService<MisafirTipiDto, MisafirTipi, int>
{
    Task<MisafirTipiYonetimBaglamDto> GetYonetimBaglamAsync(CancellationToken cancellationToken = default);

    Task<List<MisafirTipiTesisAtamaDto>> GetTesisAtamalariAsync(int tesisId, CancellationToken cancellationToken = default);

    Task<List<MisafirTipiTesisAtamaDto>> KaydetTesisAtamalariAsync(int tesisId, IReadOnlyCollection<int> misafirTipiIds, CancellationToken cancellationToken = default);

    Task<List<MisafirTipiDto>> GetAktifMisafirTipleriByTesisAsync(int tesisId, CancellationToken cancellationToken = default);
}
