using AutoMapper;
using STYS.Muhasebe.BankaHareketleri.Dtos;
using STYS.Muhasebe.BankaHareketleri.Entities;

namespace STYS.Muhasebe.BankaHareketleri.Mapping;

public class BankaHareketProfile : Profile
{
    public BankaHareketProfile()
    {
        CreateMap<BankaHareket, BankaHareketDto>().ReverseMap();
        CreateMap<CreateBankaHareketRequest, BankaHareketDto>();
        CreateMap<UpdateBankaHareketRequest, BankaHareketDto>();
    }
}
