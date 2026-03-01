using STYS.YoneticiAdaylari.Dto;

namespace STYS.YoneticiAdaylari.Services;

public interface IYoneticiAdayService
{
    Task<List<YoneticiAdayDto>> GetAllAsync(CancellationToken cancellationToken = default);
}
