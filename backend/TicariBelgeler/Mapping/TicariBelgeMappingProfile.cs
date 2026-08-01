using AutoMapper;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.TicariBelgeler.Dtos;

namespace STYS.TicariBelgeler.Mapping;

/// <summary>
/// TicariBelgeler (operasyon uygulama katmanı) DTO'ları ile Muhasebe.SatisBelgeleri DTO'ları
/// arasındaki merkezi AutoMapper eşlemesi. TicariBelgeService BU profildeki map'leri kullanır -
/// controller içinde manuel entity/DTO mapping YAPILMAZ (bkz. görev B).
/// </summary>
public class TicariBelgeMappingProfile : Profile
{
    public TicariBelgeMappingProfile()
    {
        // ── SatisBelgesiDto -> TicariBelgeDto/TicariBelgeDetayDto (operasyon çıktısı) ──
        // İşlem yetenekleri (GuncellenebilirMi vb.) ve OperasyonelDurumAciklamasi buradan
        // OTOMATİK türetilmez - TicariBelgeService, map'ten SONRA TicariBelgeIslemYetkisi
        // üzerinden merkezi olarak doldurur (bkz. TicariBelgeService.UygulaIslemYetkileri).
        CreateMap<SatisBelgesiDto, TicariBelgeDto>();
        CreateMap<SatisBelgesiDto, TicariBelgeDetayDto>();
        CreateMap<SatisBelgesiSatiriDto, TicariBelgeSatirDto>();

        // ── TicariBelgeTaslakOlusturRequest -> SatisBelgesiTaslakOlusturRequest ──
        CreateMap<TicariBelgeTaslakOlusturRequest, SatisBelgesiTaslakOlusturRequest>();
        CreateMap<TicariBelgeTaslakSatirRequest, SatisBelgesiTaslakSatirRequest>();

        // ── TicariBelgeGuncelleRequest -> UpdateSatisBelgesiRequest ──
        CreateMap<TicariBelgeGuncelleRequest, UpdateSatisBelgesiRequest>();
        CreateMap<TicariBelgeGuncelleSatirRequest, CreateSatisBelgesiSatiriRequest>();

        // ── TicariBelgeFilterDto -> SatisBelgesiFilterDto ──
        // TicariDurum/MuhasebeDurumu/FaturalamaDurumu convention ile eşlenir ve
        // SatisBelgesiService.FilterAsync tarafından SQL sorgusunda uygulanır - burada veya
        // TicariBelgeService'te bellek içi ikinci bir süzme YAPILMAZ. Legacy Durum filtresi
        // (SatisBelgesiFilterDto.Durum) BİLİNÇLİ OLARAK yok sayılır - operasyon katmanı legacy
        // Durum filtresi KULLANMAZ.
        CreateMap<TicariBelgeFilterDto, SatisBelgesiFilterDto>()
            .ForMember(dest => dest.Durum, opt => opt.Ignore());

        // GEÇİCİ TicariBelgeDetayDto -> SatisBelgesiDto reverse-compatibility mapping'i KALDIRILDI
        // (bkz. görev E) - tüm operasyon servisleri artık TicariBelgeDto/TicariBelgeDetayDto'yu
        // DOĞRUDAN döner, muhasebe namespace'indeki DTO'ları dış sözleşme olarak KULLANMAZ.
    }
}
