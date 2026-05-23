using STYS.Muhasebe.Kdv.Dtos;

namespace STYS.Muhasebe.Kdv.Services;

public interface IKdvHareketRaporService
{
    Task<KdvHareketRaporDto> GetRaporAsync(
        KdvHareketRaporFilterDto filter,
        CancellationToken cancellationToken = default);

    Task<byte[]> ExportExcelAsync(
        KdvHareketRaporFilterDto filter,
        CancellationToken cancellationToken = default);
}
