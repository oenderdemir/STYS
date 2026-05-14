using AutoMapper;
using STYS.Muhasebe.MuhasebeFisleri.Dtos;
using STYS.Muhasebe.MuhasebeFisleri.Entities;

namespace STYS.Muhasebe.MuhasebeFisleri.Mapping;

public class MuhasebeFisProfile : Profile
{
    public MuhasebeFisProfile()
    {
        CreateMap<MuhasebeFis, MuhasebeFisDto>()
            .ForMember(d => d.Satirlar, o => o.MapFrom(s => s.Satirlar));

        CreateMap<MuhasebeFisSatir, MuhasebeFisSatirDto>()
            .ForMember(d => d.MuhasebeHesapKodu, o => o.MapFrom(s => s.MuhasebeHesapPlani != null ? s.MuhasebeHesapPlani.Kod : null))
            .ForMember(d => d.MuhasebeHesapAdi, o => o.MapFrom(s => s.MuhasebeHesapPlani != null ? s.MuhasebeHesapPlani.Ad : null));

        CreateMap<MuhasebeFisDto, MuhasebeFis>()
            .ForMember(d => d.Satirlar, o => o.MapFrom(s => s.Satirlar));
        CreateMap<MuhasebeFisSatirDto, MuhasebeFisSatir>();

        CreateMap<CreateMuhasebeFisSatirRequest, MuhasebeFisSatirDto>();
        CreateMap<CreateMuhasebeFisSatirRequest, MuhasebeFisSatir>();

        CreateMap<CreateMuhasebeFisRequest, MuhasebeFisDto>();
        CreateMap<UpdateMuhasebeFisRequest, MuhasebeFisDto>();
    }
}
