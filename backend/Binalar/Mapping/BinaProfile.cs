using AutoMapper;
using STYS.Binalar.Dto;
using STYS.Binalar.Entities;

namespace STYS.Binalar.Mapping;

public class BinaProfile : Profile
{
    public BinaProfile()
    {
        CreateMap<Bina, BinaDto>()
            .ForMember(dest => dest.YoneticiUserIds, opt => opt.MapFrom(src => src.Yoneticiler.Select(x => x.UserId)));

        CreateMap<BinaDto, Bina>()
            .ForMember(dest => dest.Yoneticiler, opt => opt.Ignore());
    }
}
