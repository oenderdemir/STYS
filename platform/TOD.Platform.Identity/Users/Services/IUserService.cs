using TOD.Platform.Identity.Users.DTO;
using TOD.Platform.Identity.Users.Entities;
using TOD.Platform.Persistence.Rdbms.Services;

namespace TOD.Platform.Identity.Users.Services;

public interface IUserService : IBaseRdbmsService<UserDto, User>
{
    Task ResetPasswordAsync(Guid id, UserResetPasswordDto dto);
}
