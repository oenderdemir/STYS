using AutoMapper;
using STYS.MisafirTipleri.Dto;
using STYS.MisafirTipleri.Entities;

namespace STYS.MisafirTipleri.Mapping;

public class MisafirTipiProfile : Profile
{
    public MisafirTipiProfile()
    {
        CreateMap<MisafirTipi, MisafirTipiDto>().ReverseMap();
    }
}
