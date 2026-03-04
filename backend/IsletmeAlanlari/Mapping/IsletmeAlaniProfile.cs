using AutoMapper;
using STYS.IsletmeAlanlari.Dto;
using STYS.IsletmeAlanlari.Entities;

namespace STYS.IsletmeAlanlari.Mapping;

public class IsletmeAlaniProfile : Profile
{
    public IsletmeAlaniProfile()
    {
        CreateMap<IsletmeAlani, IsletmeAlaniDto>()
            .ForMember(dest => dest.Ad, opt => opt.MapFrom(src =>
                !string.IsNullOrWhiteSpace(src.OzelAd)
                    ? src.OzelAd
                    : src.IsletmeAlaniSinifi != null
                        ? src.IsletmeAlaniSinifi.Ad
                        : string.Empty))
            .ForMember(dest => dest.IsletmeAlaniSinifiAd, opt => opt.MapFrom(src =>
                src.IsletmeAlaniSinifi != null
                    ? src.IsletmeAlaniSinifi.Ad
                    : null));

        CreateMap<IsletmeAlaniDto, IsletmeAlani>()
            .ForMember(dest => dest.IsletmeAlaniSinifi, opt => opt.Ignore());

        CreateMap<IsletmeAlaniSinifi, IsletmeAlaniSinifiDto>().ReverseMap();
    }
}
