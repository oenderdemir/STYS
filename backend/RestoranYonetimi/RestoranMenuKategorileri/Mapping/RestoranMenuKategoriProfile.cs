using AutoMapper;
using STYS.RestoranMenuKategorileri.Dtos;
using STYS.RestoranMenuKategorileri.Entities;

namespace STYS.RestoranMenuKategorileri.Mapping;

public class RestoranMenuKategoriProfile : Profile
{
    public RestoranMenuKategoriProfile()
    {
        CreateMap<RestoranMenuKategori, RestoranMenuKategoriDto>().ReverseMap();
    }
}
