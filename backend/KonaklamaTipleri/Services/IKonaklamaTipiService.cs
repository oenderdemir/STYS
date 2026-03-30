using STYS.KonaklamaTipleri.Dto;
using STYS.KonaklamaTipleri.Entities;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.KonaklamaTipleri.Services;

public interface IKonaklamaTipiService : IBaseRdbmsService<KonaklamaTipiDto, KonaklamaTipi, int>
{
    Task<KonaklamaTipiYonetimBaglamDto> GetYonetimBaglamAsync(CancellationToken cancellationToken = default);

    Task<List<KonaklamaTipiTesisAtamaDto>> GetTesisAtamalariAsync(int tesisId, CancellationToken cancellationToken = default);

    Task<List<KonaklamaTipiTesisAtamaDto>> KaydetTesisAtamalariAsync(int tesisId, IReadOnlyCollection<int> konaklamaTipiIds, CancellationToken cancellationToken = default);

    Task<List<KonaklamaTipiTesisIcerikOverrideDto>> GetTesisIcerikOverrideAsync(int tesisId, int konaklamaTipiId, CancellationToken cancellationToken = default);

    Task<List<KonaklamaTipiTesisIcerikOverrideDto>> KaydetTesisIcerikOverrideAsync(int tesisId, int konaklamaTipiId, IReadOnlyCollection<KonaklamaTipiTesisIcerikOverrideDto> items, CancellationToken cancellationToken = default);

    Task<List<KonaklamaTipiDto>> GetAktifKonaklamaTipleriByTesisAsync(int tesisId, CancellationToken cancellationToken = default);
}
