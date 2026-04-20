using AutoMapper;
using STYS.Muhasebe.PaketTurleri.Dtos;
using STYS.Muhasebe.PaketTurleri.Entities;

namespace STYS.Muhasebe.PaketTurleri.Mapping;

public class PaketTuruProfile : Profile
{
    public PaketTuruProfile()
    {
        CreateMap<PaketTuru, PaketTuruDto>().ReverseMap();
        CreateMap<CreatePaketTuruRequest, PaketTuruDto>();
        CreateMap<UpdatePaketTuruRequest, PaketTuruDto>();
    }
}
