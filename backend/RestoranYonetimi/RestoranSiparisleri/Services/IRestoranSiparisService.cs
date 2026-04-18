using STYS.RestoranSiparisleri.Dtos;
using STYS.RestoranSiparisleri.Entities;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.RestoranSiparisleri.Services;

public interface IRestoranSiparisService : IBaseRdbmsService<RestoranSiparisDto, RestoranSiparis, int>
{
    Task<List<RestoranSiparisDto>> GetByRestoranIdAsync(int restoranId, CancellationToken cancellationToken = default);
    Task<List<RestoranSiparisDto>> GetAcikSiparislerAsync(int? masaId, CancellationToken cancellationToken = default);
    Task<RestoranSiparisDto> UpdateDurumAsync(int id, UpdateRestoranSiparisDurumRequest request, CancellationToken cancellationToken = default);
}
