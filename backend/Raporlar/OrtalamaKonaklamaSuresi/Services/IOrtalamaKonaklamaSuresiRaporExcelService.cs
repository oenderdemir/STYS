namespace STYS.Raporlar.OrtalamaKonaklamaSuresi.Services;

public interface IOrtalamaKonaklamaSuresiRaporExcelService
{
    Task<byte[]> OlusturAsync(
        int tesisId,
        DateTime baslangic,
        DateTime bitis,
        int? odaTipiId = null,
        CancellationToken cancellationToken = default);
}
