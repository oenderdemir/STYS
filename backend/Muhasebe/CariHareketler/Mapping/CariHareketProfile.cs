using AutoMapper;
using STYS.Muhasebe.CariHareketler.Dtos;
using STYS.Muhasebe.CariHareketler.Entities;

namespace STYS.Muhasebe.CariHareketler.Mapping;

public class CariHareketProfile : Profile
{
    public CariHareketProfile()
    {
        CreateMap<CariHareket, CariHareketDto>();
        CreateMap<CariHareket, CariHareketDurumOzetDto>()
            .ForMember(x => x.CariHareketId, opt => opt.MapFrom(src => src.Id));
        CreateMap<CariHareketDto, CariHareket>()
            .ForMember(x => x.KapananTutar, opt => opt.Ignore())
            .ForMember(x => x.KalanTutar, opt => opt.Ignore())
            .ForMember(x => x.IliskiliCariHareketId, opt => opt.Ignore())
            .ForMember(x => x.KapandiMi, opt => opt.Ignore())
            .ForMember(x => x.IliskiliCariHareket, opt => opt.Ignore())
            .ForMember(x => x.CariKart, opt => opt.Ignore());
        CreateMap<CreateCariHareketRequest, CariHareketDto>();
        CreateMap<UpdateCariHareketRequest, CariHareketDto>();
    }
}
