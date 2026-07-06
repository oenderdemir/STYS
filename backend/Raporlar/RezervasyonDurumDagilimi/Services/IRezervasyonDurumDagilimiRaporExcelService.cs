namespace STYS.Raporlar.RezervasyonDurumDagilimi.Services;

public interface IRezervasyonDurumDagilimiRaporExcelService
{
    Task<byte[]> OlusturAsync(
        int tesisId,
        DateTime baslangic,
        DateTime bitis,
        int? odaTipiId = null,
        string? durum = null,
        CancellationToken cancellationToken = default);
}
