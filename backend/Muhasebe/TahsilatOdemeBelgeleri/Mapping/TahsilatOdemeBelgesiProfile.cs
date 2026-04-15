using AutoMapper;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Dtos;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Entities;

namespace STYS.Muhasebe.TahsilatOdemeBelgeleri.Mapping;

public class TahsilatOdemeBelgesiProfile : Profile
{
    public TahsilatOdemeBelgesiProfile()
    {
        CreateMap<TahsilatOdemeBelgesi, TahsilatOdemeBelgesiDto>().ReverseMap();
        CreateMap<CreateTahsilatOdemeBelgesiRequest, TahsilatOdemeBelgesiDto>();
        CreateMap<UpdateTahsilatOdemeBelgesiRequest, TahsilatOdemeBelgesiDto>();
    }
}
