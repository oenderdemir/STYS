using STYS.Restoranlar.Dtos;
using TOD.Platform.Identity.Users.DTO;

namespace STYS.Restoranlar.Services;

public interface IRestoranService
{
    Task<List<RestoranDto>> GetListAsync(int? tesisId, CancellationToken cancellationToken = default);
    Task<List<RestoranIsletmeAlaniSecenekDto>> GetIsletmeAlaniSecenekleriAsync(int tesisId, CancellationToken cancellationToken = default);
    Task<RestoranDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<RestoranDto> CreateAsync(CreateRestoranRequest request, CancellationToken cancellationToken = default);
    Task<RestoranDto> UpdateAsync(int id, UpdateRestoranRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<UserDto> CreateRestoranYoneticisiUserAsync(int restoranId, UserDto dto, CancellationToken cancellationToken = default);
    Task<UserDto> CreateRestoranGarsonuUserAsync(int restoranId, UserDto dto, CancellationToken cancellationToken = default);
}
