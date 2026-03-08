using AutoMapper;
using STYS.OdaKullanimBloklari.Dto;
using STYS.OdaKullanimBloklari.Entities;

namespace STYS.OdaKullanimBloklari.Mapping;

public class OdaKullanimBlokProfile : Profile
{
    public OdaKullanimBlokProfile()
    {
        CreateMap<OdaKullanimBlok, OdaKullanimBlokDto>().ReverseMap();
    }
}

