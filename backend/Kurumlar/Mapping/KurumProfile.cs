using AutoMapper;
using STYS.Kurumlar.Dto;
using STYS.Kurumlar.Entities;

namespace STYS.Kurumlar.Mapping;

public class KurumProfile : Profile
{
    public KurumProfile()
    {
        CreateMap<Kurum, KurumDto>().ReverseMap();
        CreateMap<CreateKurumRequest, KurumDto>();
        CreateMap<UpdateKurumRequest, KurumDto>();
    }
}
