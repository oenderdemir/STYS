using AutoMapper;
using STYS.Muhasebe.CariHareketler.Dtos;
using STYS.Muhasebe.CariHareketler.Entities;

namespace STYS.Muhasebe.CariHareketler.Mapping;

public class CariHareketProfile : Profile
{
    public CariHareketProfile()
    {
        CreateMap<CariHareket, CariHareketDto>().ReverseMap();
        CreateMap<CreateCariHareketRequest, CariHareketDto>();
        CreateMap<UpdateCariHareketRequest, CariHareketDto>();
    }
}
