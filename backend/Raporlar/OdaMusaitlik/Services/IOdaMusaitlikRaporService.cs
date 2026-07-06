using STYS.Raporlar.OdaMusaitlik.Dto;

namespace STYS.Raporlar.OdaMusaitlik.Services;

public interface IOdaMusaitlikRaporService
{
    Task<OdaMusaitlikRaporDto> GetRaporAsync(
        int tesisId,
        DateTime baslangic,
        DateTime bitis,
        string? durum = null,
        int? odaTipiId = null,
        int? kapasite = null,
        CancellationToken cancellationToken = default);
}
