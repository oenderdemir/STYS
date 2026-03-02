using AutoMapper;
using TOD.Platform.Identity.MenuItems.DTO;
using TOD.Platform.Identity.MenuItems.Entities;

namespace TOD.Platform.Identity.MenuItems.Mapping;

public class MenuItemProfile : Profile
{
    public MenuItemProfile()
    {
        CreateMap<MenuItem, MenuItemDto>()
            .ForMember(dest => dest.ParentId, opt => opt.MapFrom(src => src.ParentId))
            .ForMember(dest => dest.Roles, opt => opt.MapFrom(src => src.MenuItemRoles.Where(x => x.Role != null && x.Role.Name == "Menu").Select(x => x.Role)))
            .ReverseMap()
            .ForMember(dest => dest.Parent, opt => opt.Ignore())
            .ForMember(dest => dest.Items, opt => opt.Ignore())
            .ForMember(dest => dest.MenuItemRoles, opt => opt.Ignore());
    }
}
