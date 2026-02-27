using AutoMapper;
using STYS.Binalar.Dto;
using STYS.Binalar.Entities;

namespace STYS.Binalar.Mapping;

public class BinaProfile : Profile
{
    public BinaProfile()
    {
        CreateMap<Bina, BinaDto>().ReverseMap();
    }
}