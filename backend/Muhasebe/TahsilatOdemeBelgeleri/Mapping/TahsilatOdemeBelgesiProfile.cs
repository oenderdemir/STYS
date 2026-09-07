using AutoMapper;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Dtos;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Entities;

namespace STYS.Muhasebe.TahsilatOdemeBelgeleri.Mapping;

public class TahsilatOdemeBelgesiProfile : Profile
{
    public TahsilatOdemeBelgesiProfile()
    {
        CreateMap<TahsilatOdemeBelgesi, TahsilatOdemeBelgesiDto>()
            .ForMember(d => d.MuhasebeFisDurumu, opt => opt.MapFrom(s => s.MuhasebeFis != null ? s.MuhasebeFis.Durum : null))
            .ForMember(d => d.CariKodu, opt => opt.MapFrom(s => s.CariKart != null ? s.CariKart.CariKodu : null))
            .ForMember(d => d.CariUnvanAdSoyad, opt => opt.MapFrom(s => s.CariKart != null ? s.CariKart.UnvanAdSoyad : null))
            .ForMember(d => d.CariVergiNoTckn, opt => opt.MapFrom(s => s.CariKart != null ? s.CariKart.VergiNoTckn : null))
            .ReverseMap()
            .ForMember(s => s.MuhasebeFis, opt => opt.Ignore())
            .ForMember(s => s.CariKart, opt => opt.Ignore());
        CreateMap<CreateTahsilatOdemeBelgesiRequest, TahsilatOdemeBelgesiDto>();
        CreateMap<UpdateTahsilatOdemeBelgesiRequest, TahsilatOdemeBelgesiDto>();
    }
}
