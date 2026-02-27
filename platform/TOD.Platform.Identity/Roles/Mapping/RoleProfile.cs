using AutoMapper;
using TOD.Platform.Identity.Roles.DTO;
using TOD.Platform.Identity.Roles.Entities;

namespace TOD.Platform.Identity.Roles.Mapping;

public class RoleProfile : Profile
{
    public RoleProfile()
    {
        CreateMap<Role, RoleDto>().ReverseMap();
    }
}
