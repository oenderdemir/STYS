using AutoMapper;
using System;
using STYS.Muhasebe.Depolar.Dtos;
using STYS.Muhasebe.Depolar.Entities;

namespace STYS.Muhasebe.Depolar.Mapping;

public class DepoProfile : Profile
{
    public DepoProfile()
    {
        CreateMap<DepoCikisGrup, DepoCikisGrupDto>().ReverseMap();

        CreateMap<Depo, DepoDto>()
            .ForMember(dest => dest.MalzemeKayitTipi, opt => opt.MapFrom(src => src.MalzemeKayitTipi.ToString()))
            .ForMember(dest => dest.CikisGruplari, opt => opt.MapFrom(src => src.DepoCikisGruplari));

        CreateMap<DepoDto, Depo>()
            .ForMember(dest => dest.MalzemeKayitTipi, opt => opt.MapFrom(src => Enum.Parse<DepoMalzemeKayitTipleri>(src.MalzemeKayitTipi)))
            .ForMember(dest => dest.DepoCikisGruplari, opt => opt.MapFrom(src => src.CikisGruplari));

        CreateMap<CreateDepoCikisGrupRequest, DepoCikisGrupDto>();
        CreateMap<CreateDepoRequest, DepoDto>();
        CreateMap<UpdateDepoRequest, DepoDto>();
    }
}
