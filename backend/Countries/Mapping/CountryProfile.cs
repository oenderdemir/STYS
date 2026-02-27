using AutoMapper;
using STYS.Countries.Dto;
using STYS.Countries.Entities;

namespace STYS.Countries.Mapping;

public class CountryProfile : Profile
{
    public CountryProfile()
    {
        CreateMap<Country, CountryDto>().ReverseMap();
    }
}
