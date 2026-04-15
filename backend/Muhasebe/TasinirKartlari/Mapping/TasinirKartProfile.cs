using AutoMapper;
using STYS.Muhasebe.TasinirKartlari.Dtos;
using STYS.Muhasebe.TasinirKartlari.Entities;

namespace STYS.Muhasebe.TasinirKartlari.Mapping;

public class TasinirKartProfile : Profile
{
    public TasinirKartProfile()
    {
        CreateMap<TasinirKart, TasinirKartDto>().ReverseMap();
        CreateMap<CreateTasinirKartRequest, TasinirKartDto>();
        CreateMap<UpdateTasinirKartRequest, TasinirKartDto>();
    }
}
