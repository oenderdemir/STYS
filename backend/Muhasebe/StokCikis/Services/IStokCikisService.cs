using STYS.Muhasebe.StokHareketleri.Dtos;
using STYS.Muhasebe.StokTalepleri.Dtos;

namespace STYS.Muhasebe.StokCikis.Services;

public interface IStokCikisService
{
    Task<StokTalepDto> TalepBaslatAsync(CreateStokTalepRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StokHareketDto>> DogrudanTransferBaslatAsync(StokTransferRequest request, CancellationToken cancellationToken = default);
}
