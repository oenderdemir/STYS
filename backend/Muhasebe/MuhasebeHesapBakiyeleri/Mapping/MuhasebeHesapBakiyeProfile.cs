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
            .ForMember(d => d.Bakiye, o => o.MapFrom(s => Math.Abs(s.NetBakiye)));

        CreateMap<MuhasebeHesapBakiyeDto, MuhasebeHesapBakiye>();
        CreateMap<CreateMuhasebeHesapBakiyeRequest, MuhasebeHesapBakiyeDto>();
        CreateMap<UpdateMuhasebeHesapBakiyeRequest, MuhasebeHesapBakiyeDto>();
    }
}
