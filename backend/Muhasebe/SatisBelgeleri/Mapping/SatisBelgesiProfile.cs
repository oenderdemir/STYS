using AutoMapper;
using STYS.Muhasebe.CariKartlar.Entities;
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
            .ForMember(dest => dest.CariKartId, opt => opt.MapFrom(src => src.CariKartId))
            .ForMember(dest => dest.CariKartKodu, opt => opt.MapFrom(src => src.CariKart != null ? src.CariKart.CariKodu : null))
            .ForMember(dest => dest.CariKartUnvanAdSoyad, opt => opt.MapFrom(src => src.CariKart != null ? src.CariKart.UnvanAdSoyad : null))
            .ForMember(dest => dest.CariKartTipi, opt => opt.MapFrom(src => src.CariKart != null ? src.CariKart.CariTipi : null))
            .ForMember(dest => dest.CariKartVergiNoTckn, opt => opt.MapFrom(src => src.CariKart != null ? src.CariKart.VergiNoTckn : null))
            .ForMember(dest => dest.Satirlar, opt => opt.MapFrom(src =>
                src.Satirlar
                    .Where(s => !s.IsDeleted)
                    .OrderBy(s => s.SiraNo)))
            .ForMember(dest => dest.ToplamTevkifatTutari, opt => opt.MapFrom(src =>
                src.Satirlar.Where(s => !s.IsDeleted).Sum(s => s.TevkifatTutari)))
            .ForMember(dest => dest.ToplamNetKdv, opt => opt.MapFrom(src =>
                src.Satirlar.Where(s => !s.IsDeleted).Sum(s => s.KdvTutari - s.TevkifatTutari)));

        CreateMap<SatisBelgesiDto, SatisBelgesi>()
            .ForMember(dest => dest.Satirlar, opt => opt.Ignore())
            .ForMember(dest => dest.CariKart, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.IsDeleted, opt => opt.Ignore());

        // ── SatisBelgesiSatiri <-> SatisBelgesiSatiriDto ──
        CreateMap<SatisBelgesiSatiri, SatisBelgesiSatiriDto>()
            .ForMember(dest => dest.KdvUygulamaTipi, opt => opt.MapFrom(src => (int)src.KdvUygulamaTipi))
            .ForMember(dest => dest.NetKdv, opt => opt.MapFrom(src => src.KdvTutari - src.TevkifatTutari));

        CreateMap<SatisBelgesiSatiriDto, SatisBelgesiSatiri>()
            .ForMember(dest => dest.KdvUygulamaTipi, opt => opt.MapFrom(src => (KdvUygulamaTipi)src.KdvUygulamaTipi))
            .ForMember(dest => dest.SatisBelgesi, opt => opt.Ignore())
            .ForMember(dest => dest.TasinirKart, opt => opt.Ignore())
            .ForMember(dest => dest.Depo, opt => opt.Ignore())
            .ForMember(dest => dest.KdvIstisnaTanim, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.IsDeleted, opt => opt.Ignore());

        // ── CreateSatisBelgesiRequest -> SatisBelgesi (alan bazlı manuel mapping tercih ediliyor) ──
        // ── Bu yüzden CreateSatisBelgesiRequest için map tanımlanmıyor. ──
    }
}
