using AutoMapper;
using STYS.OdaTipleri.Dto;
using STYS.OdaTipleri.Entities;

namespace STYS.OdaTipleri.Mapping;

public class OdaTipiProfile : Profile
{
    public OdaTipiProfile()
    {
        CreateMap<TesisOdaTipiOzellikDeger, TesisOdaTipiOzellikDegerDto>().ReverseMap();
        CreateMap<OdaTipi, OdaTipiDto>().ReverseMap();
    }
}
