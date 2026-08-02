using AutoMapper;
using STYS.Muhasebe.SatisBelgeleri;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Muhasebe.SatisBelgeleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Enums;

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
            .ForMember(dest => dest.EBelgeUuid, opt => opt.MapFrom(src =>
                src.EBelgeKaydi != null ? src.EBelgeKaydi.EBelgeUuid : src.EBelgeUuid))
            .ForMember(dest => dest.Satirlar, opt => opt.MapFrom(src =>
                src.Satirlar
                    .Where(s => !s.IsDeleted)
                    .OrderBy(s => s.SiraNo)))
            .ForMember(dest => dest.ToplamTevkifatTutari, opt => opt.MapFrom(src =>
                src.Satirlar.Where(s => !s.IsDeleted).Sum(s => s.TevkifatTutari)))
            .ForMember(dest => dest.ToplamNetKdv, opt => opt.MapFrom(src =>
                src.Satirlar.Where(s => !s.IsDeleted).Sum(s => s.KdvTutari - s.TevkifatTutari)))
            .ForMember(dest => dest.IadeEdilenBelgeNo, opt => opt.MapFrom(src =>
                src.IadeEdilenBelge != null ? src.IadeEdilenBelge.BelgeNo : null))
            .ForMember(dest => dest.IadeEdilenFaturaNo, opt => opt.MapFrom(src =>
                src.IadeEdilenBelge != null
                    ? (src.IadeEdilenBelge.BelgeTipi == SatisBelgesiTipi.SatisFaturasi
                        ? src.IadeEdilenBelge.ResmiFaturaNo
                        : src.IadeEdilenBelge.KarsiTarafFaturaNo)
                    : null))
            .ForMember(dest => dest.IadeEdilenBelgeTarihi, opt => opt.MapFrom(src =>
                src.IadeEdilenBelge != null ? (DateTime?)src.IadeEdilenBelge.BelgeTarihi : null))
            .ForMember(dest => dest.IadeEdilenBelgeTipi, opt => opt.MapFrom(src =>
                src.IadeEdilenBelge != null ? (SatisBelgesiTipi?)src.IadeEdilenBelge.BelgeTipi : null))
            // ── İşlem yetenekleri — TEK merkezi kaynaktan (TicariBelgeIslemYetkisi) türetilir,
            // legacy Durum ASLA kullanılmaz (bkz. görev 2). Her mapping çağrısında (GetByIdAsync,
            // FilterAsync, Create/Update sonrası dönüş) OTOMATİK olarak hesaplanır. ──
            .ForMember(dest => dest.GuncellenebilirMi, opt => opt.MapFrom(src =>
                TicariBelgeIslemYetkisi.GuncellenebilirMi(src.TicariDurum, src.MuhasebeDurumu)))
            .ForMember(dest => dest.SilinebilirMi, opt => opt.MapFrom(src =>
                TicariBelgeIslemYetkisi.SilinebilirMi(src.TicariDurum, src.MuhasebeDurumu, src.FaturalamaDurumu)))
            .ForMember(dest => dest.MuhasebeOnayinaGonderilebilirMi, opt => opt.MapFrom(src =>
                TicariBelgeIslemYetkisi.MuhasebeOnayinaGonderilebilirMi(src.TicariDurum, src.MuhasebeDurumu)))
            .ForMember(dest => dest.MuhasebeOnaylanabilirMi, opt => opt.MapFrom(src =>
                TicariBelgeIslemYetkisi.MuhasebeOnaylanabilirMi(src.TicariDurum, src.MuhasebeDurumu)))
            .ForMember(dest => dest.ReddedilebilirMi, opt => opt.MapFrom(src =>
                TicariBelgeIslemYetkisi.ReddedilebilirMi(src.TicariDurum, src.MuhasebeDurumu)))
            .ForMember(dest => dest.IptalEdilebilirMi, opt => opt.MapFrom(src =>
                TicariBelgeIslemYetkisi.IptalEdilebilirMi(src.TicariDurum, src.FaturalamaDurumu)))
            .ForMember(dest => dest.MuhasebeFisiOlusturulabilirMi, opt => opt.MapFrom(src =>
                TicariBelgeIslemYetkisi.MuhasebeFisiOlusturulabilirMi(src.MuhasebeDurumu, src.MuhasebeFisId, src.BelgeTipi)));

        CreateMap<SatisBelgesiDto, SatisBelgesi>()
            .ForMember(dest => dest.Satirlar, opt => opt.Ignore())
            .ForMember(dest => dest.CariKart, opt => opt.Ignore())
            .ForMember(dest => dest.EBelgeKaydi, opt => opt.Ignore())
            .ForMember(dest => dest.EBelgeUuid, opt => opt.Ignore())
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
