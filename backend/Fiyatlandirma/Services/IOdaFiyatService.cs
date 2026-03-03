using STYS.Fiyatlandirma.Dto;
using STYS.Fiyatlandirma.Entities;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.Fiyatlandirma.Services;

public interface IOdaFiyatService : IBaseRdbmsService<OdaFiyatDto, OdaFiyat, int>
{
    Task<List<OdaFiyatDto>> GetByTesisOdaTipiIdAsync(int tesisOdaTipiId, CancellationToken cancellationToken = default);

    Task<List<OdaFiyatDto>> UpsertByTesisOdaTipiAsync(int tesisOdaTipiId, IEnumerable<OdaFiyatDto> fiyatlar, CancellationToken cancellationToken = default);

    Task<OdaFiyatHesaplamaSonucuDto> HesaplaAsync(OdaFiyatHesaplaRequestDto request, CancellationToken cancellationToken = default);
}
