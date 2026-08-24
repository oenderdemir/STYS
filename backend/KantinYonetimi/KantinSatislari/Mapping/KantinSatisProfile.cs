using AutoMapper;
using STYS.KantinYonetimi.KantinSatislari.Dtos;
using STYS.KantinYonetimi.KantinSatislari.Entities;

namespace STYS.KantinYonetimi.KantinSatislari.Mapping;

public class KantinSatisProfile : Profile
{
    public KantinSatisProfile()
    {
        CreateMap<KantinSatis, KantinSatisDto>();
        CreateMap<KantinSatisDto, KantinSatis>()
            .ForMember(x => x.Kantin, opt => opt.Ignore())
            .ForMember(x => x.MuhasebeFis, opt => opt.Ignore())
            .ForMember(x => x.Satirlar, opt => opt.Ignore())
            .ForMember(x => x.Odemeler, opt => opt.Ignore());

        CreateMap<KantinSatisSatir, KantinSatisSatirDto>();
        CreateMap<KantinSatisSatirDto, KantinSatisSatir>()
            .ForMember(x => x.KantinSatis, opt => opt.Ignore())
            .ForMember(x => x.KantinUrun, opt => opt.Ignore())
            .ForMember(x => x.TasinirKart, opt => opt.Ignore())
            .ForMember(x => x.StokLot, opt => opt.Ignore())
            .ForMember(x => x.StokSeri, opt => opt.Ignore())
            .ForMember(x => x.StokHareket, opt => opt.Ignore());

        CreateMap<KantinSatisOdeme, KantinSatisOdemeDto>();
        CreateMap<KantinSatisOdemeDto, KantinSatisOdeme>()
            .ForMember(x => x.KantinSatis, opt => opt.Ignore())
            .ForMember(x => x.KasaBankaHesap, opt => opt.Ignore());
    }
}
