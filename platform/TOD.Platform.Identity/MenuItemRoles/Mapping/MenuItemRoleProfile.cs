using AutoMapper;
using TOD.Platform.Identity.MenuItemRoles.DTO;
using TOD.Platform.Identity.MenuItemRoles.Entities;

namespace TOD.Platform.Identity.MenuItemRoles.Mapping;

public class MenuItemRoleProfile : Profile
{
    public MenuItemRoleProfile()
    {
        CreateMap<MenuItemRole, MenuItemRoleDto>().ReverseMap();
    }
}
