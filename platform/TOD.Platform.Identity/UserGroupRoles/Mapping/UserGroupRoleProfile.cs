using AutoMapper;
using TOD.Platform.Identity.UserGroupRoles.DTO;
using TOD.Platform.Identity.UserGroupRoles.Entities;

namespace TOD.Platform.Identity.UserGroupRoles.Mapping;

public class UserGroupRoleProfile : Profile
{
    public UserGroupRoleProfile()
    {
        CreateMap<UserGroupRole, UserGroupRoleDto>().ReverseMap();
    }
}
