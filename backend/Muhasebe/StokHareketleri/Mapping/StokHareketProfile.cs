using AutoMapper;
using STYS.Muhasebe.StokHareketleri.Dtos;
using STYS.Muhasebe.StokHareketleri.Entities;

namespace STYS.Muhasebe.StokHareketleri.Mapping;

public class StokHareketProfile : Profile
{
    public StokHareketProfile()
    {
        CreateMap<StokHareket, StokHareketDto>().ReverseMap();
        CreateMap<CreateStokHareketRequest, StokHareketDto>();
        CreateMap<UpdateStokHareketRequest, StokHareketDto>();
        CreateMap<StokTransferRequest, StokHareketDto>()
            .ForMember(dest => dest.DepoId, opt => opt.MapFrom(src => src.KaynakDepoId))
            .ForMember(dest => dest.HareketTipi, opt => opt.MapFrom(_ => StokHareketTipleri.Transfer));
    }
}
