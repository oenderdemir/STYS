using AutoMapper;
using STYS.Kurumlar.Dto;
using STYS.Kurumlar.Entities;

namespace STYS.Kurumlar.Mapping;

public class KurumProfile : Profile
{
    public KurumProfile()
    {
        // LogoUrl is computed in the controller and not stored on the entity.
        // AutoMapper silently ignores source members with no matching destination.
        CreateMap<Kurum, KurumDto>().ReverseMap();
        CreateMap<CreateKurumRequest, KurumDto>();
        CreateMap<UpdateKurumRequest, KurumDto>();
    }
}
