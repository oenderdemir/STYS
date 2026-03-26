using AutoMapper;
using STYS.EkHizmetler.Dto;
using STYS.EkHizmetler.Entities;

namespace STYS.EkHizmetler.Mapping;

public class EkHizmetTarifeProfile : Profile
{
    public EkHizmetTarifeProfile()
    {
        CreateMap<EkHizmet, EkHizmetDto>().ReverseMap();

        CreateMap<EkHizmetTarife, EkHizmetTarifeDto>()
            .ForMember(dest => dest.EkHizmetAdi, opt => opt.MapFrom(src => src.EkHizmet != null ? src.EkHizmet.Ad : string.Empty))
            .ForMember(dest => dest.EkHizmetAciklama, opt => opt.MapFrom(src => src.EkHizmet != null ? src.EkHizmet.Aciklama : null))
            .ForMember(dest => dest.BirimAdi, opt => opt.MapFrom(src => src.EkHizmet != null ? src.EkHizmet.BirimAdi : string.Empty));

        CreateMap<EkHizmetTarifeDto, EkHizmetTarife>()
            .ForMember(dest => dest.EkHizmet, opt => opt.Ignore())
            .ForMember(dest => dest.Tesis, opt => opt.Ignore())
            .ForMember(dest => dest.RezervasyonEkHizmetleri, opt => opt.Ignore());
    }
}
