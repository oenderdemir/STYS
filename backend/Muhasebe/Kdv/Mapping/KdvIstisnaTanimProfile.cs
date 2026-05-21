using AutoMapper;
using STYS.Muhasebe.Kdv.Dtos;
using STYS.Muhasebe.Kdv.Entities;

namespace STYS.Muhasebe.Kdv.Mapping;

public class KdvIstisnaTanimProfile : Profile
{
    public KdvIstisnaTanimProfile()
    {
        CreateMap<KdvIstisnaTanim, KdvIstisnaTanimDto>();
        CreateMap<CreateKdvIstisnaTanimRequest, KdvIstisnaTanimDto>();
        CreateMap<UpdateKdvIstisnaTanimRequest, KdvIstisnaTanimDto>();
        CreateMap<KdvIstisnaTanimDto, KdvIstisnaTanim>();
    }
}
