using AutoMapper;
using STYS.Muhasebe.MuhasebeHesapBakiyeleri.Dtos;
using STYS.Muhasebe.MuhasebeHesapBakiyeleri.Entities;

namespace STYS.Muhasebe.MuhasebeHesapBakiyeleri.Mapping;

public class MuhasebeHesapBakiyeProfile : Profile
{
    public MuhasebeHesapBakiyeProfile()
    {
        CreateMap<MuhasebeHesapBakiye, MuhasebeHesapBakiyeDto>()
            .ForMember(d => d.TesisAdi, o => o.MapFrom(s => s.Tesis != null ? s.Tesis.Ad : null))
            .ForMember(d => d.Bakiye, o => o.MapFrom(s => Math.Abs(s.BorcToplam - s.AlacakToplam)))
            .ForMember(d => d.BakiyeTipi, o => o.MapFrom(s =>
                s.BorcToplam > s.AlacakToplam ? "Borc" :
                s.AlacakToplam > s.BorcToplam ? "Alacak" :
                "Sifir"));

        CreateMap<MuhasebeHesapBakiyeDto, MuhasebeHesapBakiye>();
        CreateMap<CreateMuhasebeHesapBakiyeRequest, MuhasebeHesapBakiyeDto>();
        CreateMap<UpdateMuhasebeHesapBakiyeRequest, MuhasebeHesapBakiyeDto>();
    }
}
