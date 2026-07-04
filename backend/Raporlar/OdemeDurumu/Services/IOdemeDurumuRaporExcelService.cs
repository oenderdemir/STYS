namespace STYS.Raporlar.OdemeDurumu.Services;

public interface IOdemeDurumuRaporExcelService
{
    Task<byte[]> OlusturAsync(
        int tesisId,
        DateTime baslangic,
        DateTime bitis,
        string? odemeDurumu,
        CancellationToken cancellationToken = default);
}
