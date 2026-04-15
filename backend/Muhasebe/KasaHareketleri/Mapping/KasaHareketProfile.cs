using AutoMapper;
using STYS.Muhasebe.KasaHareketleri.Dtos;
using STYS.Muhasebe.KasaHareketleri.Entities;

namespace STYS.Muhasebe.KasaHareketleri.Mapping;

public class KasaHareketProfile : Profile
{
    public KasaHareketProfile()
    {
        CreateMap<KasaHareket, KasaHareketDto>().ReverseMap();
        CreateMap<CreateKasaHareketRequest, KasaHareketDto>();
        CreateMap<UpdateKasaHareketRequest, KasaHareketDto>();
    }
}
