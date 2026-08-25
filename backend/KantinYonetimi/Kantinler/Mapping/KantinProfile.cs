using AutoMapper;
using STYS.KantinYonetimi.Kantinler.Dtos;
using STYS.KantinYonetimi.Kantinler.Entities;

namespace STYS.KantinYonetimi.Kantinler.Mapping;

public class KantinProfile : Profile
{
    public KantinProfile()
    {
        CreateMap<Kantin, KantinDto>();
        CreateMap<KantinDto, Kantin>()
            .ForMember(x => x.Tesis, opt => opt.Ignore())
            .ForMember(x => x.Depo, opt => opt.Ignore())
            .ForMember(x => x.VarsayilanNakitKasa, opt => opt.Ignore())
            .ForMember(x => x.VarsayilanPosHesap, opt => opt.Ignore())
            .ForMember(x => x.Urunler, opt => opt.Ignore());

        CreateMap<KantinUrun, KantinUrunDto>();
        CreateMap<KantinUrunDto, KantinUrun>()
            .ForMember(x => x.Kantin, opt => opt.Ignore())
            .ForMember(x => x.TasinirKart, opt => opt.Ignore());
    }
}
