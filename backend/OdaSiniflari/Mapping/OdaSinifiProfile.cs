using AutoMapper;
using STYS.OdaSiniflari.Dto;
using STYS.OdaSiniflari.Entities;

namespace STYS.OdaSiniflari.Mapping;

public class OdaSinifiProfile : Profile
{
    public OdaSinifiProfile()
    {
        CreateMap<OdaSinifi, OdaSinifiDto>().ReverseMap();
    }
}
