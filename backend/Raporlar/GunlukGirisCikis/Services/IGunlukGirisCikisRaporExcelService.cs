namespace STYS.Raporlar.GunlukGirisCikis.Services;

public interface IGunlukGirisCikisRaporExcelService
{
    Task<byte[]> OlusturAsync(
        int tesisId,
        DateTime tarih,
        string? listeTipi = null,
        CancellationToken cancellationToken = default);
}
