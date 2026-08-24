using AutoMapper;
using STYS.Muhasebe.SarfFisleri.Dtos;
using STYS.Muhasebe.SarfFisleri.Entities;

namespace STYS.Muhasebe.SarfFisleri.Mapping;

public class SarfFisiProfile : Profile
{
    public SarfFisiProfile()
    {
        CreateMap<SarfFisi, SarfFisiDto>()
            .ForMember(dest => dest.IsletmeAlaniAd, opt => opt.MapFrom(src => src.IsletmeAlaniAdSnapshot))
            .ForMember(dest => dest.BirimAd, opt => opt.MapFrom(src => src.IsletmeAlaniAdSnapshot))
            .ForMember(dest => dest.OdaAd, opt => opt.MapFrom(src =>
                !string.IsNullOrWhiteSpace(src.OdaNoSnapshot)
                    ? !string.IsNullOrWhiteSpace(src.OdaBinaAdiSnapshot)
                        ? $"{src.OdaNoSnapshot} - {src.OdaBinaAdiSnapshot}"
                        : src.OdaNoSnapshot
                    : null));
        CreateMap<SarfFisiDto, SarfFisi>();
        CreateMap<SarfFisiSatir, SarfFisiSatirDto>();
        CreateMap<SarfFisiSatirDto, SarfFisiSatir>();
        CreateMap<CreateSarfFisiRequest, SarfFisiDto>();
    }
}
