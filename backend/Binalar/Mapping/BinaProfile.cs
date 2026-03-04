using AutoMapper;
using STYS.Binalar.Dto;
using STYS.Binalar.Entities;
using STYS.IsletmeAlanlari.Entities;

namespace STYS.Binalar.Mapping;

public class BinaProfile : Profile
{
    public BinaProfile()
    {
        CreateMap<Bina, BinaDto>()
            .ForMember(dest => dest.YoneticiUserIds, opt => opt.MapFrom(src => src.Yoneticiler.Select(x => x.UserId)))
            .ForMember(dest => dest.IsletmeAlanlari, opt => opt.MapFrom(src => src.IsletmeAlanlari));

        CreateMap<BinaDto, Bina>()
            .ForMember(dest => dest.Yoneticiler, opt => opt.Ignore())
            .ForMember(dest => dest.IsletmeAlanlari, opt => opt.Ignore());

        CreateMap<IsletmeAlani, BinaIsletmeAlaniDto>()
            .ForMember(dest => dest.IsletmeAlaniSinifiAd, opt => opt.MapFrom(src =>
                src.IsletmeAlaniSinifi != null
                    ? src.IsletmeAlaniSinifi.Ad
                    : null));
    }
}
