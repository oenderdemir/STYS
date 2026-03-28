using AutoMapper;
using STYS.KonaklamaTipleri;
using STYS.KonaklamaTipleri.Dto;
using STYS.KonaklamaTipleri.Entities;
using System.Globalization;

namespace STYS.KonaklamaTipleri.Mapping;

public class KonaklamaTipiProfile : Profile
{
    public KonaklamaTipiProfile()
    {
        CreateMap<KonaklamaTipiIcerikKalemi, KonaklamaTipiIcerikDto>()
            .ForMember(dest => dest.HizmetAdi, opt => opt.MapFrom(src => KonaklamaTipiIcerikHizmetKodlari.GetAd(src.HizmetKodu)))
            .ForMember(dest => dest.PeriyotAdi, opt => opt.MapFrom(src => KonaklamaTipiIcerikPeriyotlari.GetAd(src.Periyot)))
            .ForMember(dest => dest.KullanimTipiAdi, opt => opt.MapFrom(src => KonaklamaTipiIcerikKullanimTipleri.GetAd(src.KullanimTipi)))
            .ForMember(dest => dest.KullanimNoktasiAdi, opt => opt.MapFrom(src => KonaklamaTipiIcerikKullanimNoktalari.GetAd(src.KullanimNoktasi)))
            .ForMember(dest => dest.KullanimBaslangicSaati, opt => opt.MapFrom(src => src.KullanimBaslangicSaati.HasValue ? src.KullanimBaslangicSaati.Value.ToString(@"hh\:mm", CultureInfo.InvariantCulture) : null))
            .ForMember(dest => dest.KullanimBitisSaati, opt => opt.MapFrom(src => src.KullanimBitisSaati.HasValue ? src.KullanimBitisSaati.Value.ToString(@"hh\:mm", CultureInfo.InvariantCulture) : null));

        CreateMap<KonaklamaTipiIcerikDto, KonaklamaTipiIcerikKalemi>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.KonaklamaTipiId, opt => opt.Ignore())
            .ForMember(dest => dest.KonaklamaTipi, opt => opt.Ignore())
            .ForMember(dest => dest.KullanimBaslangicSaati, opt => opt.MapFrom(src => ParseTime(src.KullanimBaslangicSaati)))
            .ForMember(dest => dest.KullanimBitisSaati, opt => opt.MapFrom(src => ParseTime(src.KullanimBitisSaati)))
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedBy, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedBy, opt => opt.Ignore())
            .ForMember(dest => dest.DeletedAt, opt => opt.Ignore())
            .ForMember(dest => dest.DeletedBy, opt => opt.Ignore())
            .ForMember(dest => dest.IsDeleted, opt => opt.Ignore());

        CreateMap<KonaklamaTipi, KonaklamaTipiDto>();
        CreateMap<KonaklamaTipiDto, KonaklamaTipi>()
            .ForMember(dest => dest.IcerikKalemleri, opt => opt.Ignore());
    }

    private static TimeSpan? ParseTime(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? null
            : TimeSpan.ParseExact(value, @"hh\:mm", CultureInfo.InvariantCulture);
}
