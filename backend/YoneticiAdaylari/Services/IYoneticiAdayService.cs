using STYS.YoneticiAdaylari.Dto;

namespace STYS.YoneticiAdaylari.Services;

public interface IYoneticiAdayService
{
    Task<List<YoneticiAdayDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<List<YoneticiAdayDto>> GetTesisYoneticiAdaylariAsync(CancellationToken cancellationToken = default);

    Task<List<YoneticiAdayDto>> GetBinaYoneticiAdaylariAsync(CancellationToken cancellationToken = default);
    Task<List<YoneticiAdayDto>> GetRestoranYoneticiAdaylariAsync(CancellationToken cancellationToken = default);
    Task<List<YoneticiAdayDto>> GetRestoranGarsonAdaylariAsync(CancellationToken cancellationToken = default);

    Task<List<YoneticiAdayDto>> GetResepsiyonistAdaylariAsync(CancellationToken cancellationToken = default);
}
