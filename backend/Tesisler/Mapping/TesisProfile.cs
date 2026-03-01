using AutoMapper;
using STYS.Tesisler.Dto;
using STYS.Tesisler.Entities;

namespace STYS.Tesisler.Mapping;

public class TesisProfile : Profile
{
    public TesisProfile()
    {
        CreateMap<Tesis, TesisDto>()
            .ForMember(dest => dest.YoneticiUserIds, opt => opt.MapFrom(src => src.Yoneticiler.Select(x => x.UserId)));

        CreateMap<TesisDto, Tesis>()
            .ForMember(dest => dest.Yoneticiler, opt => opt.Ignore());
    }
}
