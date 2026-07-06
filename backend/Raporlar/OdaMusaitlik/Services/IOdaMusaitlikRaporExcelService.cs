namespace STYS.Raporlar.OdaMusaitlik.Services;

public interface IOdaMusaitlikRaporExcelService
{
    Task<byte[]> OlusturAsync(
        int tesisId,
        DateTime baslangic,
        DateTime bitis,
        string? durum = null,
        int? odaTipiId = null,
        int? kapasite = null,
        CancellationToken cancellationToken = default);
}
