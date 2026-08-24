using AutoMapper;
using STYS.Muhasebe.SarfFisleri.Dtos;
using STYS.Muhasebe.SarfFisleri.Entities;

namespace STYS.Muhasebe.SarfFisleri.Mapping;

public class SarfFisiProfile : Profile
{
    public SarfFisiProfile()
    {
        CreateMap<SarfFisi, SarfFisiDto>()
            .ForMember(dest => dest.IsletmeAlaniAd, opt => opt.MapFrom(src => ResolveIsletmeAlaniAdi(src)))
            .ForMember(dest => dest.BirimAd, opt => opt.MapFrom(src => ResolveIsletmeAlaniAdi(src)))
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

    private static string? ResolveIsletmeAlaniAdi(SarfFisi src)
    {
        if (!string.IsNullOrWhiteSpace(src.IsletmeAlaniAdSnapshot))
        {
            return src.IsletmeAlaniAdSnapshot;
        }

        if (!string.IsNullOrWhiteSpace(src.IsletmeAlani?.OzelAd))
        {
            return src.IsletmeAlani.OzelAd;
        }

        return src.IsletmeAlani?.IsletmeAlaniSinifi?.Ad;
    }
}
