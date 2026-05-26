using STYS.Muhasebe.CariHareketler.Dtos;
using STYS.Muhasebe.CariHareketler.Entities;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.Muhasebe.CariHareketler.Services;

public interface ICariHareketService : IBaseRdbmsService<CariHareketDto, CariHareket, int>
{
    Task<CariBakiyeOzetDto> GetCariBakiyeOzetAsync(int cariKartId, CancellationToken cancellationToken = default);
    Task<List<CariHareketDurumOzetDto>> GetCariAcikHareketlerAsync(int cariKartId, CancellationToken cancellationToken = default);
    Task<List<CariHareketDurumOzetDto>> GetCariKapananHareketlerAsync(int cariKartId, CancellationToken cancellationToken = default);
    Task<List<CariHareketDurumOzetDto>> GetCariHareketEkstreAsync(int cariKartId, DateTime? baslangic, DateTime? bitis, CancellationToken cancellationToken = default);
    Task<CariEkstreDto> GetEkstreAsync(int cariKartId, DateTime? baslangic, DateTime? bitis, CancellationToken cancellationToken = default);
}
