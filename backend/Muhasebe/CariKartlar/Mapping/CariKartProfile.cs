using AutoMapper;
using STYS.Muhasebe.CariKartlar.Dtos;
using STYS.Muhasebe.CariKartlar.Entities;

namespace STYS.Muhasebe.CariKartlar.Mapping;

public class CariKartProfile : Profile
{
    public CariKartProfile()
    {
        CreateMap<CariKartBankaHesabi, CariKartBankaHesabiDto>().ReverseMap();
        CreateMap<CariKartYetkiliKisi, CariKartYetkiliKisiDto>().ReverseMap();
        CreateMap<CariKart, CariKartDto>()
            .ForMember(x => x.BankaHesaplari, opt => opt.MapFrom(src => src.BankaHesaplari.Where(b => !b.IsDeleted)))
            .ReverseMap()
            .ForMember(x => x.BankaHesaplari, opt => opt.Ignore())
            .ForMember(x => x.YetkiliKisiler, opt => opt.Ignore());
        CreateMap<CreateCariKartRequest, CariKartDto>();
        CreateMap<UpdateCariKartRequest, CariKartDto>();
    }
}
