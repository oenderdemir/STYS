using STYS.Muhasebe.StokHareketleri.Dtos;
using STYS.Muhasebe.StokHareketleri.Entities;
using STYS.Muhasebe.StokLotlari.Dtos;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.Muhasebe.StokHareketleri.Services;

public interface IStokHareketService : IBaseRdbmsService<StokHareketDto, StokHareket, int>
{
    Task<StokHareketDto> AddWithinCurrentTransactionAsync(StokHareketDto dto, CancellationToken cancellationToken = default);
    Task<List<StokBakiyeDto>> GetStokBakiyeAsync(int? tesisId, int? depoId, CancellationToken cancellationToken = default);
    Task<List<StokKartOzetDto>> GetStokKartOzetAsync(int? tesisId, int? depoId, CancellationToken cancellationToken = default);
    Task<StokDetayDto> GetStokDetayAsync(int depoId, int tasinirKartId, CancellationToken cancellationToken = default);
    Task<List<StokLotBakiyeDto>> GetLotBakiyeleriAsync(int depoId, int tasinirKartId, CancellationToken cancellationToken = default);
    Task<List<StokSeriBakiyeDto>> GetSeriBakiyeleriAsync(int depoId, int tasinirKartId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StokHareketDto>> CreateTransferAsync(StokTransferRequest request, CancellationToken cancellationToken = default);
    Task TransferIptalAsync(int id, CancellationToken cancellationToken = default);
}
