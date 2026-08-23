using AutoMapper;
using STYS.Muhasebe.StokHareketleri.Dtos;
using STYS.Muhasebe.StokHareketleri.Entities;

namespace STYS.Muhasebe.StokHareketleri.Mapping;

public class StokHareketProfile : Profile
{
    public StokHareketProfile()
    {
        CreateMap<StokHareket, StokHareketDto>()
            .ForMember(dest => dest.LotNo, opt => opt.MapFrom(src => src.StokLot != null ? src.StokLot.LotNo : null))
            .ForMember(dest => dest.SonKullanmaTarihi, opt => opt.MapFrom(src => src.StokLot != null ? src.StokLot.SonKullanmaTarihi : null));

        CreateMap<StokHareketDto, StokHareket>()
            .ForMember(dest => dest.Depo, opt => opt.Ignore())
            .ForMember(dest => dest.KarsiDepo, opt => opt.Ignore())
            .ForMember(dest => dest.TasinirKart, opt => opt.Ignore())
            .ForMember(dest => dest.StokLot, opt => opt.Ignore())
            .ForMember(dest => dest.CariKart, opt => opt.Ignore())
            .ForMember(dest => dest.KdvIstisnaTanim, opt => opt.Ignore());

        CreateMap<CreateStokHareketRequest, StokHareketDto>();
        CreateMap<UpdateStokHareketRequest, StokHareketDto>();
        CreateMap<StokTransferRequest, StokHareketDto>()
            .ForMember(dest => dest.DepoId, opt => opt.MapFrom(src => src.KaynakDepoId))
            .ForMember(dest => dest.HareketTipi, opt => opt.MapFrom(_ => StokHareketTipleri.Transfer));
    }
}
