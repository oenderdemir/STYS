using STYS.Muhasebe.Kdv.Dtos;

namespace STYS.Muhasebe.Kdv.Services;

public interface IKdvOzetRaporService
{
    Task<KdvOzetRaporDto> GetOzetRaporAsync(
        KdvOzetRaporFilterDto filter,
        CancellationToken cancellationToken = default);

    Task<byte[]> ExportExcelAsync(
        KdvOzetRaporFilterDto filter,
        CancellationToken cancellationToken = default);
}
