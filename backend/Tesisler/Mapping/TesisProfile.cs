using AutoMapper;
using System.Globalization;
using STYS.Tesisler.Dto;
using STYS.Tesisler.Entities;

namespace STYS.Tesisler.Mapping;

public class TesisProfile : Profile
{
    public TesisProfile()
    {
        CreateMap<Tesis, TesisDto>()
            .ForMember(dest => dest.GirisSaati, opt => opt.MapFrom(src => FormatSaat(src.GirisSaati)))
            .ForMember(dest => dest.CikisSaati, opt => opt.MapFrom(src => FormatSaat(src.CikisSaati)))
            .ForMember(dest => dest.YoneticiUserIds, opt => opt.MapFrom(src => src.Yoneticiler.Select(x => x.UserId)))
            .ForMember(dest => dest.ResepsiyonistUserIds, opt => opt.MapFrom(src => src.Resepsiyonistler.Select(x => x.UserId)));

        CreateMap<TesisDto, Tesis>()
            .ForMember(dest => dest.GirisSaati, opt => opt.MapFrom(src => ParseSaat(src.GirisSaati, new TimeSpan(14, 0, 0))))
            .ForMember(dest => dest.CikisSaati, opt => opt.MapFrom(src => ParseSaat(src.CikisSaati, new TimeSpan(10, 0, 0))))
            .ForMember(dest => dest.Yoneticiler, opt => opt.Ignore())
            .ForMember(dest => dest.Resepsiyonistler, opt => opt.Ignore());
    }

    private static string FormatSaat(TimeSpan time)
    {
        return time.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
    }

    private static TimeSpan ParseSaat(string? timeText, TimeSpan defaultValue)
    {
        if (string.IsNullOrWhiteSpace(timeText))
        {
            return defaultValue;
        }

        if (TimeSpan.TryParseExact(timeText.Trim(), @"hh\:mm", CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return defaultValue;
    }
}
