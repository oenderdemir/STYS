using AutoMapper;
using TOD.Platform.Identity.UserGroups.DTO;
using TOD.Platform.Identity.UserGroups.Entities;

namespace TOD.Platform.Identity.UserGroups.Mapping;

public class UserGroupProfile : Profile
{
    public UserGroupProfile()
    {
        CreateMap<UserGroup, UserGroupDto>()
            .ForMember(dest => dest.Roles, opt => opt.MapFrom(src => src.UserGroupRoles.Select(x => x.Role)))
            .ReverseMap()
            .ForMember(dest => dest.UserGroupRoles, opt => opt.Ignore())
            .ForMember(dest => dest.UserUserGroups, opt => opt.Ignore());
    }
}
