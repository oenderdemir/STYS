using AutoMapper;
using STYS.RestoranMasalari.Dtos;
using STYS.RestoranMasalari.Entities;

namespace STYS.RestoranMasalari.Mapping;

public class RestoranMasaProfile : Profile
{
    public RestoranMasaProfile()
    {
        CreateMap<RestoranMasa, RestoranMasaDto>().ReverseMap();
    }
}
