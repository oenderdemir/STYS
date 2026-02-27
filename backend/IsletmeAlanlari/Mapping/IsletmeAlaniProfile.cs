using AutoMapper;
using STYS.IsletmeAlanlari.Dto;
using STYS.IsletmeAlanlari.Entities;

namespace STYS.IsletmeAlanlari.Mapping;

public class IsletmeAlaniProfile : Profile
{
    public IsletmeAlaniProfile()
    {
        CreateMap<IsletmeAlani, IsletmeAlaniDto>().ReverseMap();
    }
}