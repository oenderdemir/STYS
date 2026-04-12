using STYS.MusteriMenu.Dtos;

namespace STYS.MusteriMenu.Services;

public interface IMusteriMenuService
{
    Task<MusteriMenuDto> GetByRestoranIdAsync(int restoranId, CancellationToken cancellationToken = default);
}
