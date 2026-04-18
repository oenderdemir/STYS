using AutoMapper;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Dtos;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;

namespace STYS.Muhasebe.MuhasebeHesapPlanlari.Mapping;

public class MuhasebeHesapPlaniProfile : Profile
{
    public MuhasebeHesapPlaniProfile()
    {
        CreateMap<MuhasebeHesapPlani, MuhasebeHesapPlaniDto>().ReverseMap();
        CreateMap<CreateMuhasebeHesapPlaniRequest, MuhasebeHesapPlaniDto>();
        CreateMap<UpdateMuhasebeHesapPlaniRequest, MuhasebeHesapPlaniDto>();
    }
}
