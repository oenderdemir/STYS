using AutoMapper;
using STYS.Odalar.Dto;
using STYS.Odalar.Entities;
using STYS.OdaOzellikleri.Entities;

namespace STYS.Odalar.Mapping;

public class OdaProfile : Profile
{
    public OdaProfile()
    {
        CreateMap<OdaOzellikDeger, OdaOzellikDegerDto>().ReverseMap();
        CreateMap<Oda, OdaDto>().ReverseMap();
    }
}
