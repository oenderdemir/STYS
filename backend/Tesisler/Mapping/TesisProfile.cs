using AutoMapper;
using STYS.Tesisler.Dto;
using STYS.Tesisler.Entities;

namespace STYS.Tesisler.Mapping;

public class TesisProfile : Profile
{
    public TesisProfile()
    {
        CreateMap<Tesis, TesisDto>().ReverseMap();
    }
}