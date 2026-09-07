using AutoMapper;
using STYS.Muhasebe.KonaklamaVergisiHesapEslemeleri.Dtos;
using STYS.Muhasebe.KonaklamaVergisiHesapEslemeleri.Entities;

namespace STYS.Muhasebe.KonaklamaVergisiHesapEslemeleri.Mapping;

public class KonaklamaVergisiHesapEslemeProfile : Profile
{
    public KonaklamaVergisiHesapEslemeProfile()
    {
        CreateMap<KonaklamaVergisiHesapEsleme, KonaklamaVergisiHesapEslemeDto>()
            .ForMember(d => d.TesisAdi, o => o.MapFrom(s => s.Tesis != null ? s.Tesis.Ad : null))
            .ForMember(d => d.VergiHesapKodu, o => o.MapFrom(s => s.VergiHesap != null ? s.VergiHesap.TamKod : null))
            .ForMember(d => d.VergiHesapAdi, o => o.MapFrom(s => s.VergiHesap != null ? s.VergiHesap.Ad : null));

        CreateMap<KonaklamaVergisiHesapEslemeDto, KonaklamaVergisiHesapEsleme>();
        CreateMap<CreateKonaklamaVergisiHesapEslemeRequest, KonaklamaVergisiHesapEslemeDto>();
        CreateMap<UpdateKonaklamaVergisiHesapEslemeRequest, KonaklamaVergisiHesapEslemeDto>();
    }
}

