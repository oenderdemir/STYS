using AutoMapper;
using STYS.KonaklamaTipleri.Dto;
using STYS.KonaklamaTipleri.Entities;

namespace STYS.KonaklamaTipleri.Mapping;

public class KonaklamaTipiProfile : Profile
{
    public KonaklamaTipiProfile()
    {
        CreateMap<KonaklamaTipi, KonaklamaTipiDto>().ReverseMap();
    }
}
