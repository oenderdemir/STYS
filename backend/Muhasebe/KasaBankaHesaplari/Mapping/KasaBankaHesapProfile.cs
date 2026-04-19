using AutoMapper;
using STYS.Muhasebe.KasaBankaHesaplari.Dtos;
using STYS.Muhasebe.KasaBankaHesaplari.Entities;

namespace STYS.Muhasebe.KasaBankaHesaplari.Mapping;

public class KasaBankaHesapProfile : Profile
{
    public KasaBankaHesapProfile()
    {
        CreateMap<KasaBankaHesap, KasaBankaHesapDto>()
            .ForMember(d => d.MuhasebeTamKod, opt => opt.MapFrom(s => s.MuhasebeHesapPlani != null ? s.MuhasebeHesapPlani.TamKod : null))
            .ForMember(d => d.MuhasebeHesapAdi, opt => opt.MapFrom(s => s.MuhasebeHesapPlani != null ? s.MuhasebeHesapPlani.Ad : null));

        CreateMap<KasaBankaHesapDto, KasaBankaHesap>();
        CreateMap<CreateKasaBankaHesapRequest, KasaBankaHesapDto>();
        CreateMap<UpdateKasaBankaHesapRequest, KasaBankaHesapDto>();
    }
}
