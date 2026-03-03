using AutoMapper;
using STYS.Fiyatlandirma.Dto;
using STYS.Fiyatlandirma.Entities;

namespace STYS.Fiyatlandirma.Mapping;

public class IndirimKuraliProfile : Profile
{
    public IndirimKuraliProfile()
    {
        CreateMap<IndirimKurali, IndirimKuraliDto>()
            .ForMember(dest => dest.MisafirTipiIds, opt => opt.MapFrom(src => src.MisafirTipiKisitlari.Select(x => x.MisafirTipiId)))
            .ForMember(dest => dest.KonaklamaTipiIds, opt => opt.MapFrom(src => src.KonaklamaTipiKisitlari.Select(x => x.KonaklamaTipiId)));

        CreateMap<IndirimKuraliDto, IndirimKurali>()
            .ForMember(dest => dest.MisafirTipiKisitlari, opt => opt.Ignore())
            .ForMember(dest => dest.KonaklamaTipiKisitlari, opt => opt.Ignore());
    }
}
