using STYS.Raporlar.GecikenCheckIn.Dto;

namespace STYS.Raporlar.GecikenCheckIn.Services;

public interface IGecikenCheckInRaporService
{
    Task<GecikenCheckInRaporDto> GetRaporAsync(
        int tesisId,
        DateTime? referansTarihi = null,
        int? odaTipiId = null,
        string? gecikmeDurumu = null,
        CancellationToken cancellationToken = default);
}
