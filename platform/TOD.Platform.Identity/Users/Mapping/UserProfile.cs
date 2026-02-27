using AutoMapper;
using TOD.Platform.Identity.Users.DTO;
using TOD.Platform.Identity.Users.Entities;

namespace TOD.Platform.Identity.Users.Mapping;

public class UserProfile : Profile
{
    public UserProfile()
    {
        CreateMap<User, UserDto>()
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()))
            .ForMember(dest => dest.UserGroups, opt => opt.MapFrom(src => src.UserUserGroups.Select(x => x.UserGroup)))
            .ReverseMap()
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => Enum.Parse<Common.Enums.UserStatus>(src.Status, true)))
            .ForMember(dest => dest.UserUserGroups, opt => opt.Ignore());
    }
}
