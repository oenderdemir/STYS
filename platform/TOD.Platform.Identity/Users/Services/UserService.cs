using AutoMapper;
using TOD.Platform.Identity.Users.DTO;
using TOD.Platform.Identity.Users.Entities;
using TOD.Platform.Identity.Users.Repositories;
using TOD.Platform.Persistence.RDBMS.Services;

namespace TOD.Platform.Identity.Users.Services;

public class UserService : BaseService<UserDto, User>, IUserService
{
    public UserService(IUserRepository userRepository, IMapper mapper)
        : base(userRepository, mapper)
    {
    }
}
