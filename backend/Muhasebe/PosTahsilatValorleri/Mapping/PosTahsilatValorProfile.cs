using AutoMapper;
using STYS.Muhasebe.PosTahsilatValorleri.Dtos;
using STYS.Muhasebe.PosTahsilatValorleri.Entities;

namespace STYS.Muhasebe.PosTahsilatValorleri.Mapping;

public class PosTahsilatValorProfile : Profile
{
    public PosTahsilatValorProfile()
    {
        CreateMap<PosTahsilatValor, PosTahsilatValorDto>()
            .ForMember(d => d.TahsilatBelgeNo, opt => opt.MapFrom(s => s.TahsilatOdemeBelgesi != null ? s.TahsilatOdemeBelgesi.BelgeNo : null))
            .ForMember(d => d.KrediKartiHesapAdi, opt => opt.MapFrom(s => s.KrediKartiHesap != null ? s.KrediKartiHesap.Ad : null))
            .ForMember(d => d.BagliBankaHesapAdi, opt => opt.MapFrom(s => s.BagliBankaHesap != null ? s.BagliBankaHesap.Ad : null))
            .ForMember(d => d.ValoreKalanGun, opt => opt.Ignore())
            .ForMember(d => d.ValorGectiMi, opt => opt.Ignore())
            .ForMember(d => d.BugunValorGunuMu, opt => opt.Ignore())
            .ForMember(d => d.AktarilabilirMi, opt => opt.Ignore());
    }
}
