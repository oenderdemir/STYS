using AutoMapper;
using STYS.Muhasebe.SarfFisleri.Dtos;
using STYS.Muhasebe.SarfFisleri.Entities;

namespace STYS.Muhasebe.SarfFisleri.Mapping;

public class SarfFisiProfile : Profile
{
    public SarfFisiProfile()
    {
        CreateMap<SarfFisi, SarfFisiDto>()
            .ForMember(dest => dest.BirimAd, opt => opt.MapFrom(src => src.IsletmeAlani != null ? src.IsletmeAlani.OzelAd ?? src.IsletmeAlani.IsletmeAlaniSinifi!.Ad : null));
        CreateMap<SarfFisiDto, SarfFisi>();
        CreateMap<SarfFisiSatir, SarfFisiSatirDto>();
        CreateMap<SarfFisiSatirDto, SarfFisiSatir>();
        CreateMap<CreateSarfFisiRequest, SarfFisiDto>();
    }
}
