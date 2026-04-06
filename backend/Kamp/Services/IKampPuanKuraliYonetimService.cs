using STYS.Kamp.Dto;

namespace STYS.Kamp.Services;

public interface IKampPuanKuraliYonetimService
{
    Task<KampPuanKuraliYonetimBaglamDto> GetBaglamAsync(CancellationToken cancellationToken = default);

    Task<KampPuanKuraliYonetimBaglamDto> KaydetAsync(KampPuanKuraliYonetimKaydetRequestDto request, CancellationToken cancellationToken = default);
}
