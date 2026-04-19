using AutoMapper;
using STYS.Muhasebe.Hesaplar.Dtos;
using STYS.Muhasebe.Hesaplar.Entities;

namespace STYS.Muhasebe.Hesaplar.Mapping;

public class HesapProfile : Profile
{
    public HesapProfile()
    {
        CreateMap<Hesap, HesapDto>()
            .ForMember(d => d.MuhasebeTamKod, opt => opt.MapFrom(s => s.MuhasebeHesapPlani != null ? s.MuhasebeHesapPlani.TamKod : null))
            .ForMember(d => d.MuhasebeHesapAdi, opt => opt.MapFrom(s => s.MuhasebeHesapPlani != null ? s.MuhasebeHesapPlani.Ad : null));
        CreateMap<HesapDto, Hesap>()
            .ForMember(d => d.KasaBankaBaglantilari, opt => opt.Ignore())
            .ForMember(d => d.DepoBaglantilari, opt => opt.Ignore());

        CreateMap<CreateHesapRequest, HesapDto>();
        CreateMap<UpdateHesapRequest, HesapDto>();
    }
}
