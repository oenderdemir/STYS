using STYS.Raporlar.OrtalamaKonaklamaSuresi.Dto;

namespace STYS.Raporlar.OrtalamaKonaklamaSuresi.Services;

public interface IOrtalamaKonaklamaSuresiRaporService
{
    Task<OrtalamaKonaklamaSuresiRaporDto> GetRaporAsync(
        int tesisId,
        DateTime baslangic,
        DateTime bitis,
        int? odaTipiId = null,
        CancellationToken cancellationToken = default);
}
