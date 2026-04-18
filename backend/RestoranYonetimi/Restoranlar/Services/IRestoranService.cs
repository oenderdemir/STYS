using STYS.Restoranlar.Dtos;
using STYS.Restoranlar.Entities;
using TOD.Platform.Identity.Users.DTO;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.Restoranlar.Services;

public interface IRestoranService : IBaseRdbmsService<RestoranDto, Restoran, int>
{
    Task<List<RestoranIsletmeAlaniSecenekDto>> GetIsletmeAlaniSecenekleriAsync(int tesisId, CancellationToken cancellationToken = default);
    Task<UserDto> CreateRestoranYoneticisiUserAsync(int restoranId, UserDto dto, CancellationToken cancellationToken = default);
    Task<UserDto> CreateRestoranGarsonuUserAsync(int restoranId, UserDto dto, CancellationToken cancellationToken = default);
}
