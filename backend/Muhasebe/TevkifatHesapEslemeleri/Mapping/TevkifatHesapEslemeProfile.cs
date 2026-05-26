using AutoMapper;
using STYS.Muhasebe.TevkifatHesapEslemeleri.Dtos;
using STYS.Muhasebe.TevkifatHesapEslemeleri.Entities;

namespace STYS.Muhasebe.TevkifatHesapEslemeleri.Mapping;

public class TevkifatHesapEslemeProfile : Profile
{
    public TevkifatHesapEslemeProfile()
    {
        CreateMap<TevkifatHesapEsleme, TevkifatHesapEslemeDto>()
            .ForMember(d => d.TesisAdi, o => o.MapFrom(s => s.Tesis != null ? s.Tesis.Ad : null))
            .ForMember(d => d.MuhasebeHesapKodu, o => o.MapFrom(s => s.MuhasebeHesapPlani != null ? s.MuhasebeHesapPlani.Kod : null))
            .ForMember(d => d.MuhasebeHesapAdi, o => o.MapFrom(s => s.MuhasebeHesapPlani != null ? s.MuhasebeHesapPlani.Ad : null));

        CreateMap<TevkifatHesapEslemeDto, TevkifatHesapEsleme>();
        CreateMap<CreateTevkifatHesapEslemeRequest, TevkifatHesapEslemeDto>();
        CreateMap<UpdateTevkifatHesapEslemeRequest, TevkifatHesapEslemeDto>();
    }
}
