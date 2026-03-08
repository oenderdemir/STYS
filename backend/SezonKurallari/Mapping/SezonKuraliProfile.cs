using AutoMapper;
using STYS.SezonKurallari.Dto;
using STYS.SezonKurallari.Entities;

namespace STYS.SezonKurallari.Mapping;

public class SezonKuraliProfile : Profile
{
    public SezonKuraliProfile()
    {
        CreateMap<SezonKurali, SezonKuraliDto>().ReverseMap();
    }
}
