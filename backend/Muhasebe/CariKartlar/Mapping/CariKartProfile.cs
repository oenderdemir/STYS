using AutoMapper;
using STYS.Muhasebe.CariKartlar.Dtos;
using STYS.Muhasebe.CariKartlar.Entities;

namespace STYS.Muhasebe.CariKartlar.Mapping;

public class CariKartProfile : Profile
{
    public CariKartProfile()
    {
        CreateMap<CariKart, CariKartDto>().ReverseMap();
        CreateMap<CreateCariKartRequest, CariKartDto>();
        CreateMap<UpdateCariKartRequest, CariKartDto>();
    }
}
