namespace STYS.Raporlar.GecikenCheckIn.Services;

public interface IGecikenCheckInRaporExcelService
{
    Task<byte[]> OlusturAsync(
        int tesisId,
        DateTime? referansTarihi = null,
        int? odaTipiId = null,
        string? gecikmeDurumu = null,
        CancellationToken cancellationToken = default);
}
