using AutoMapper;
using STYS.Fiyatlandirma.Dto;
using STYS.Fiyatlandirma.Entities;

namespace STYS.Fiyatlandirma.Mapping;

public class OdaFiyatProfile : Profile
{
    public OdaFiyatProfile()
    {
        CreateMap<OdaFiyat, OdaFiyatDto>().ReverseMap();
    }
}
