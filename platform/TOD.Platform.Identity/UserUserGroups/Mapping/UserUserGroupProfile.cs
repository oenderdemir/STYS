using AutoMapper;
using TOD.Platform.Identity.UserUserGroups.DTO;
using TOD.Platform.Identity.UserUserGroups.Entities;

namespace TOD.Platform.Identity.UserUserGroups.Mapping;

public class UserUserGroupProfile : Profile
{
    public UserUserGroupProfile()
    {
        CreateMap<UserUserGroup, UserUserGroupDto>().ReverseMap();
    }
}
