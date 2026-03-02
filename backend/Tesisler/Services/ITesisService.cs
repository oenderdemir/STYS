using STYS.Tesisler.Dto;
using STYS.Tesisler.Entities;
using TOD.Platform.Identity.Users.DTO;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.Tesisler.Services;

public interface ITesisService : IBaseRdbmsService<TesisDto, Tesis, int>
{
    Task<UserDto> CreateResepsiyonistUserAsync(int tesisId, UserDto dto);
}
