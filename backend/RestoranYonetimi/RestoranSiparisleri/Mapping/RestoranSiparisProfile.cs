using AutoMapper;
using STYS.RestoranSiparisleri.Dtos;
using STYS.RestoranSiparisleri.Entities;

namespace STYS.RestoranSiparisleri.Mapping;

public class RestoranSiparisProfile : Profile
{
    public RestoranSiparisProfile()
    {
        CreateMap<RestoranSiparisKalemi, RestoranSiparisKalemiDto>();
        CreateMap<RestoranSiparis, RestoranSiparisDto>();
    }
}
