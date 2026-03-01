using AutoMapper;
using STYS.OdaOzellikleri.Dto;
using STYS.OdaOzellikleri.Entities;

namespace STYS.OdaOzellikleri.Mapping;

public class OdaOzellikProfile : Profile
{
    public OdaOzellikProfile()
    {
        CreateMap<OdaOzellik, OdaOzellikDto>().ReverseMap();
    }
}
