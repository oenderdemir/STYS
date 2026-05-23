using AutoMapper;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Muhasebe.SatisBelgeleri.Entities;

namespace STYS.Muhasebe.SatisBelgeleri.Mapping;

public class SatisBelgesiProfile : Profile
{
    public SatisBelgesiProfile()
    {
        // ── SatisBelgesi <-> SatisBelgesiDto ──
        CreateMap<SatisBelgesi, SatisBelgesiDto>()
            .ForMember(dest => dest.Satirlar, opt => opt.MapFrom(src =>
                src.Satirlar
                    .Where(s => !s.IsDeleted)
                    .OrderBy(s => s.SiraNo)));

        CreateMap<SatisBelgesiDto, SatisBelgesi>()
            .ForMember(dest => dest.Satirlar, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.IsDeleted, opt => opt.Ignore());

        // ── SatisBelgesiSatiri <-> SatisBelgesiSatiriDto ──
        CreateMap<SatisBelgesiSatiri, SatisBelgesiSatiriDto>()
            .ForMember(dest => dest.KdvUygulamaTipi, opt => opt.MapFrom(src => (int)src.KdvUygulamaTipi));

        CreateMap<SatisBelgesiSatiriDto, SatisBelgesiSatiri>()
            .ForMember(dest => dest.KdvUygulamaTipi, opt => opt.MapFrom(src => (KdvUygulamaTipi)src.KdvUygulamaTipi))
            .ForMember(dest => dest.SatisBelgesi, opt => opt.Ignore())
            .ForMember(dest => dest.KdvIstisnaTanim, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.IsDeleted, opt => opt.Ignore());

        // ── CreateSatisBelgesiRequest -> SatisBelgesi (alan bazlı manuel mapping tercih ediliyor) ──
        // ── Bu yüzden CreateSatisBelgesiRequest için map tanımlanmıyor. ──
    }
}
