using AutoMapper;
using STYS.EkHizmetler.Dto;
using STYS.EkHizmetler.Entities;

namespace STYS.EkHizmetler.Mapping;

public class EkHizmetTarifeProfile : Profile
{
    public EkHizmetTarifeProfile()
    {
        CreateMap<EkHizmetTarife, EkHizmetTarifeDto>().ReverseMap();
    }
}
