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
            .ReverseMap()
            .ForMember(s => s.MuhasebeFis, opt => opt.Ignore());
        CreateMap<CreateTahsilatOdemeBelgesiRequest, TahsilatOdemeBelgesiDto>();
        CreateMap<UpdateTahsilatOdemeBelgesiRequest, TahsilatOdemeBelgesiDto>();
    }
}
