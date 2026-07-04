using STYS.Raporlar.OdemeDurumu.Dto;

namespace STYS.Raporlar.OdemeDurumu.Services;

public interface IOdemeDurumuRaporService
{
    Task<OdemeDurumuRaporDto> GetRaporAsync(
        int tesisId,
        DateTime baslangic,
        DateTime bitis,
        string? odemeDurumu,
        CancellationToken cancellationToken = default);
}
