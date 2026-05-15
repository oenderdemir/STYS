using AutoMapper;
using STYS.Muhasebe.MuhasebeDonemleri.Dtos;
using STYS.Muhasebe.MuhasebeDonemleri.Entities;

namespace STYS.Muhasebe.MuhasebeDonemleri.Mapping;

public class MuhasebeDonemProfile : Profile
{
    public MuhasebeDonemProfile()
    {
        CreateMap<MuhasebeDonem, MuhasebeDonemDto>()
            .ForMember(d => d.TesisAdi, o => o.MapFrom(s => s.Tesis != null ? s.Tesis.Ad : null));

        CreateMap<CreateMuhasebeDonemRequest, MuhasebeDonemDto>();
        CreateMap<UpdateMuhasebeDonemRequest, MuhasebeDonemDto>();
        CreateMap<MuhasebeDonemDto, MuhasebeDonem>();
    }
}
