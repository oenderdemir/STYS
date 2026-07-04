namespace STYS.Raporlar.KonaklamaKisiSayisi.Services;

public interface IKonaklamaKisiSayisiRaporExcelService
{
    Task<byte[]> OlusturAsync(
        int tesisId,
        int ay,
        int baslangicYil,
        int bitisYil,
        CancellationToken cancellationToken = default);
}
