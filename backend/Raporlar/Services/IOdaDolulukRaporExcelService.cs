namespace STYS.Raporlar.Services;

public interface IOdaDolulukRaporExcelService
{
    Task<byte[]> OlusturAsync(
        int tesisId,
        int yil,
        int ay,
        bool maskele = false,
        CancellationToken cancellationToken = default);
}
