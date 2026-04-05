using AutoMapper;
using STYS.Kamp.Dto;
using STYS.Kamp.Entities;

namespace STYS.Kamp.Mapping;

public class KampProfile : Profile
{
    public KampProfile()
    {
        CreateMap<KampProgrami, KampProgramiDto>().ReverseMap();

        CreateMap<KampDonemi, KampDonemiDto>()
            .ForMember(dest => dest.KampProgramiAd, opt => opt.MapFrom(src => src.KampProgrami != null ? src.KampProgrami.Ad : null));

        CreateMap<KampDonemiDto, KampDonemi>()
            .ForMember(dest => dest.KampProgrami, opt => opt.Ignore())
            .ForMember(dest => dest.TesisAtamalari, opt => opt.Ignore());
    }
}
