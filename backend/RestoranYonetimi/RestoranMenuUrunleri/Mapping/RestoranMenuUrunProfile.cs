using AutoMapper;
using STYS.RestoranMenuUrunleri.Dtos;
using STYS.RestoranMenuUrunleri.Entities;

namespace STYS.RestoranMenuUrunleri.Mapping;

public class RestoranMenuUrunProfile : Profile
{
    public RestoranMenuUrunProfile()
    {
        CreateMap<RestoranMenuUrun, RestoranMenuUrunDto>().ReverseMap();
    }
}
