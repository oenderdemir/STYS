using AutoMapper;
using STYS.RestoranOdemeleri.Dtos;
using STYS.RestoranOdemeleri.Entities;

namespace STYS.RestoranOdemeleri.Mapping;

public class RestoranOdemeProfile : Profile
{
    public RestoranOdemeProfile()
    {
        CreateMap<RestoranOdeme, RestoranOdemeDto>().ReverseMap();
    }
}
