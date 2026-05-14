using AutoMapper;
using STYS.Muhasebe.MuhasebeVergiHesapEslemeleri.Dtos;
using STYS.Muhasebe.MuhasebeVergiHesapEslemeleri.Entities;

namespace STYS.Muhasebe.MuhasebeVergiHesapEslemeleri.Mapping;

public class MuhasebeVergiHesapEslemeProfile : Profile
{
    public MuhasebeVergiHesapEslemeProfile()
    {
        CreateMap<MuhasebeVergiHesapEsleme, MuhasebeVergiHesapEslemeDto>()
            .ForMember(d => d.AlisKdvHesapKodu, o => o.MapFrom(s => s.AlisKdvHesap != null ? s.AlisKdvHesap.Kod : null))
            .ForMember(d => d.AlisKdvHesapAdi, o => o.MapFrom(s => s.AlisKdvHesap != null ? s.AlisKdvHesap.Ad : null))
            .ForMember(d => d.SatisKdvHesapKodu, o => o.MapFrom(s => s.SatisKdvHesap != null ? s.SatisKdvHesap.Kod : null))
            .ForMember(d => d.SatisKdvHesapAdi, o => o.MapFrom(s => s.SatisKdvHesap != null ? s.SatisKdvHesap.Ad : null));

        CreateMap<MuhasebeVergiHesapEslemeDto, MuhasebeVergiHesapEsleme>();
        CreateMap<CreateMuhasebeVergiHesapEslemeRequest, MuhasebeVergiHesapEslemeDto>();
        CreateMap<UpdateMuhasebeVergiHesapEslemeRequest, MuhasebeVergiHesapEslemeDto>();
    }
}
