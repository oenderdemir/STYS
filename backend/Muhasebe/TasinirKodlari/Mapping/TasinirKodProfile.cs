using AutoMapper;
using STYS.Muhasebe.TasinirKodlari.Dtos;
using STYS.Muhasebe.TasinirKodlari.Entities;

namespace STYS.Muhasebe.TasinirKodlari.Mapping;

public class TasinirKodProfile : Profile
{
    public TasinirKodProfile()
    {
        CreateMap<TasinirKod, TasinirKodDto>().ReverseMap();
        CreateMap<CreateTasinirKodRequest, TasinirKodDto>();
        CreateMap<UpdateTasinirKodRequest, TasinirKodDto>();
    }
}
