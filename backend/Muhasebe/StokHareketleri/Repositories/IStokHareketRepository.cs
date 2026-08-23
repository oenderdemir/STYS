using STYS.Muhasebe.StokHareketleri.Dtos;
using STYS.Muhasebe.Depolar.Entities;
using STYS.Muhasebe.StokHareketleri.Entities;
using STYS.Muhasebe.StokLotlari.Dtos;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Muhasebe.StokHareketleri.Repositories;

public interface IStokHareketRepository : IBaseRdbmsRepository<StokHareket, int>
{
    Task<List<StokBakiyeDto>> GetDepoStokBakiyeleriAsync(IEnumerable<int>? depoIds, CancellationToken cancellationToken = default);
    Task<List<StokKartOzetDto>> GetStokKartOzetleriAsync(IEnumerable<int>? depoIds, CancellationToken cancellationToken = default);
    Task<List<StokDegerlemeDto>> GetStokDegerlemeAsync(IEnumerable<int>? depoIds, CancellationToken cancellationToken = default);
    Task<decimal> GetBakiyeMiktariAsync(int depoId, int tasinirKartId, CancellationToken cancellationToken = default);
    Task<decimal> GetLotBakiyeMiktariAsync(int depoId, int tasinirKartId, int stokLotId, CancellationToken cancellationToken = default);
    Task<StokDetayDto> GetStokDetayAsync(int depoId, int tasinirKartId, DepoMalzemeKayitTipleri malzemeKayitTipi, CancellationToken cancellationToken = default);
    Task<List<StokLotBakiyeDto>> GetLotBakiyeleriAsync(int depoId, int tasinirKartId, CancellationToken cancellationToken = default);
    Task<List<StokSeriBakiyeDto>> GetSeriBakiyeleriAsync(int depoId, int tasinirKartId, CancellationToken cancellationToken = default);
}
