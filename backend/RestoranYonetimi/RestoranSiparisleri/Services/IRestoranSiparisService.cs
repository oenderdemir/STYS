using STYS.RestoranSiparisleri.Dtos;

namespace STYS.RestoranSiparisleri.Services;

public interface IRestoranSiparisService
{
    Task<List<RestoranSiparisDto>> GetListAsync(int? restoranId, CancellationToken cancellationToken = default);
    Task<List<RestoranSiparisDto>> GetByRestoranIdAsync(int restoranId, CancellationToken cancellationToken = default);
    Task<List<RestoranSiparisDto>> GetAcikSiparislerAsync(int? masaId, CancellationToken cancellationToken = default);
    Task<RestoranSiparisDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<RestoranSiparisDto> CreateAsync(CreateRestoranSiparisRequest request, CancellationToken cancellationToken = default);
    Task<RestoranSiparisDto> UpdateAsync(int id, UpdateRestoranSiparisRequest request, CancellationToken cancellationToken = default);
    Task<RestoranSiparisDto> UpdateDurumAsync(int id, UpdateRestoranSiparisDurumRequest request, CancellationToken cancellationToken = default);
}
