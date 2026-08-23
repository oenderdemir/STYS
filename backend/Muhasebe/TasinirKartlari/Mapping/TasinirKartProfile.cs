using AutoMapper;
using STYS.Muhasebe.TasinirKartlari.Dtos;
using STYS.Muhasebe.TasinirKartlari.Entities;
using STYS.Muhasebe.TasinirKartlari.Services;

namespace STYS.Muhasebe.TasinirKartlari.Mapping;

public class TasinirKartProfile : Profile
{
    public TasinirKartProfile()
    {
        CreateMap<TasinirKart, TasinirKartDto>()
            .ForMember(dest => dest.TakipliMi, opt => opt.MapFrom(src => TasinirKartServiceHelpers.ResolveTakipliMi(src.TakipTipi, src.TakipliMi)));
        CreateMap<TasinirKartDto, TasinirKart>()
            .ForMember(dest => dest.TakipliMi, opt => opt.MapFrom(src => TasinirKartServiceHelpers.ResolveTakipliMi(src.TakipTipi, src.TakipliMi)));
        CreateMap<CreateTasinirKartRequest, TasinirKartDto>();
        CreateMap<UpdateTasinirKartRequest, TasinirKartDto>();
    }
}
