using AutoMapper;
using STYS.Muhasebe.Depolar.Dtos;
using STYS.Muhasebe.Depolar.Entities;

namespace STYS.Muhasebe.Depolar.Mapping;

public class DepoProfile : Profile
{
    public DepoProfile()
    {
        CreateMap<Depo, DepoDto>().ReverseMap();
        CreateMap<CreateDepoRequest, DepoDto>();
        CreateMap<UpdateDepoRequest, DepoDto>();
    }
}
