using STYS.Raporlar.OdaTipiDoluluk.Dto;

namespace STYS.Raporlar.OdaTipiDoluluk.Services;

public interface IOdaTipiDolulukRaporService
{
    Task<OdaTipiDolulukRaporDto> GetRaporAsync(
        int tesisId,
        DateTime baslangic,
        DateTime bitis,
        int? odaTipiId = null,
        CancellationToken cancellationToken = default);
}
