using STYS.Raporlar.RezervasyonDurumDagilimi.Dto;

namespace STYS.Raporlar.RezervasyonDurumDagilimi.Services;

public interface IRezervasyonDurumDagilimiRaporService
{
    Task<RezervasyonDurumDagilimiRaporDto> GetRaporAsync(
        int tesisId,
        DateTime baslangic,
        DateTime bitis,
        int? odaTipiId = null,
        string? durum = null,
        CancellationToken cancellationToken = default);
}
