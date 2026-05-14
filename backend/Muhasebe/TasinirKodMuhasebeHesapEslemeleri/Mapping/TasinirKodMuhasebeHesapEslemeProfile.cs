using AutoMapper;
using STYS.Muhasebe.TasinirKodMuhasebeHesapEslemeleri.Dtos;
using STYS.Muhasebe.TasinirKodMuhasebeHesapEslemeleri.Entities;

namespace STYS.Muhasebe.TasinirKodMuhasebeHesapEslemeleri.Mapping;

public class TasinirKodMuhasebeHesapEslemeProfile : Profile
{
    public TasinirKodMuhasebeHesapEslemeProfile()
    {
        CreateMap<TasinirKodMuhasebeHesapEsleme, TasinirKodMuhasebeHesapEslemeDto>()
            .ForMember(d => d.TasinirKodKod, o => o.MapFrom(s => s.TasinirKod != null ? s.TasinirKod.Kod : null))
            .ForMember(d => d.TasinirKodAd, o => o.MapFrom(s => s.TasinirKod != null ? s.TasinirKod.Ad : null))
            .ForMember(d => d.MuhasebeHesapKod, o => o.MapFrom(s => s.MuhasebeHesapPlani != null ? s.MuhasebeHesapPlani.Kod : null))
            .ForMember(d => d.MuhasebeHesapAd, o => o.MapFrom(s => s.MuhasebeHesapPlani != null ? s.MuhasebeHesapPlani.Ad : null));
        CreateMap<TasinirKodMuhasebeHesapEslemeDto, TasinirKodMuhasebeHesapEsleme>();
        CreateMap<CreateTasinirKodMuhasebeHesapEslemeRequest, TasinirKodMuhasebeHesapEslemeDto>();
        CreateMap<UpdateTasinirKodMuhasebeHesapEslemeRequest, TasinirKodMuhasebeHesapEslemeDto>();
    }
}