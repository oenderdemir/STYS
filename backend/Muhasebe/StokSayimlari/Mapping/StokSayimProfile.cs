using AutoMapper;
using STYS.Muhasebe.StokSayimlari.Dtos;
using STYS.Muhasebe.StokSayimlari.Entities;

namespace STYS.Muhasebe.StokSayimlari.Mapping;

public class StokSayimProfile : Profile
{
    public StokSayimProfile()
    {
        CreateMap<StokSayim, StokSayimDto>();
        CreateMap<StokSayimDto, StokSayim>();
        CreateMap<StokSayimSatir, StokSayimSatirDto>();
        CreateMap<StokSayimSatirDto, StokSayimSatir>();
        CreateMap<CreateStokSayimRequest, StokSayimDto>();
    }
}
