using AutoMapper;
using TOD.Platform.Identity.UserKurums.Dto;
using TOD.Platform.Identity.UserKurums.Entities;

namespace TOD.Platform.Identity.UserKurums.Mapping;

public class UserKurumProfile : Profile
{
    public UserKurumProfile()
    {
        CreateMap<UserKurum, UserKurumDto>().ReverseMap();
        CreateMap<AssignUserKurumRequest, UserKurum>();
        CreateMap<UpdateUserKurumRequest, UserKurum>();
    }
}
