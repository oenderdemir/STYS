using AutoMapper;
using STYS.Iller.Dto;
using STYS.Iller.Entities;

namespace STYS.Iller.Mapping;

public class IlProfile : Profile
{
    public IlProfile()
    {
        CreateMap<Il, IlDto>().ReverseMap();
    }
}