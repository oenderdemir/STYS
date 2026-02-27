using AutoMapper;
using STYS.Odalar.Dto;
using STYS.Odalar.Entities;

namespace STYS.Odalar.Mapping;

public class OdaProfile : Profile
{
    public OdaProfile()
    {
        CreateMap<Oda, OdaDto>().ReverseMap();
    }
}