using STYS.Tesisler.Dto;
using STYS.Tesisler.Entities;
using TOD.Platform.Identity.Users.DTO;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.Tesisler.Services;

public interface ITesisService : IBaseRdbmsService<TesisDto, Tesis, int>
{
    Task<UserDto> CreateResepsiyonistUserAsync(int tesisId, UserDto dto);
    Task<UserDto> CreateBinaYoneticisiUserAsync(int tesisId, UserDto dto);
    Task<UserDto> CreateRestoranYoneticisiUserAsync(int tesisId, UserDto dto);
    Task<UserDto> CreateRestoranGarsonuUserAsync(int tesisId, UserDto dto);
}
