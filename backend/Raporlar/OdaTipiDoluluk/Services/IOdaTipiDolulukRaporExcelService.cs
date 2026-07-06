namespace STYS.Raporlar.OdaTipiDoluluk.Services;

public interface IOdaTipiDolulukRaporExcelService
{
    Task<byte[]> OlusturAsync(
        int tesisId,
        DateTime baslangic,
        DateTime bitis,
        int? odaTipiId = null,
        CancellationToken cancellationToken = default);
}
