using AutoMapper;
using STYS.Muhasebe.StokTalepleri.Dtos;
using STYS.Muhasebe.StokTalepleri.Entities;

namespace STYS.Muhasebe.StokTalepleri.Mapping;

public class StokTalepProfile : Profile
{
    public StokTalepProfile()
    {
        CreateMap<StokTalep, StokTalepDto>();
        CreateMap<StokTalepDto, StokTalep>();
        CreateMap<StokTalepSatir, StokTalepSatirDto>();
        CreateMap<StokTalepSatirDto, StokTalepSatir>();
        CreateMap<CreateStokTalepRequest, StokTalepDto>();
    }
}
