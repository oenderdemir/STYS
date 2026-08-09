using AutoMapper;
using STYS.Entegrasyonlar.Pos.Dtos;
using STYS.Entegrasyonlar.Pos.Entities;

namespace STYS.Entegrasyonlar.Pos.Mapping;

public class PosCihaziProfile : Profile
{
    public PosCihaziProfile()
    {
        CreateMap<PosCihazi, PosCihaziDto>().ReverseMap();
        CreateMap<PosCihaziKaydetRequest, PosCihaziDto>();
    }
}
