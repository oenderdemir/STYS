using STYS.Muhasebe.SarfRaporlari.Dtos;

namespace STYS.Muhasebe.SarfRaporlari.Services;

public interface ISarfTuketimRaporExcelService
{
    Task<byte[]> ExportDetayAsync(SarfTuketimRaporFilterDto filter, CancellationToken cancellationToken = default);
    Task<byte[]> ExportMalzemeOzetAsync(SarfTuketimRaporFilterDto filter, CancellationToken cancellationToken = default);
    Task<byte[]> ExportKullanimYeriOzetAsync(SarfTuketimRaporFilterDto filter, CancellationToken cancellationToken = default);
}
