using AutoMapper;
using STYS.Restoranlar.Dtos;
using STYS.Restoranlar.Entities;

namespace STYS.Restoranlar.Mapping;

public class RestoranProfile : Profile
{
    public RestoranProfile()
    {
        CreateMap<Restoran, RestoranDto>().ReverseMap();
    }
}
