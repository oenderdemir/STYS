using STYS.Kamp.Dto;
using STYS.Kamp.Entities;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.Kamp.Services;

public interface IKampDonemiService : IBaseRdbmsService<KampDonemiDto, KampDonemi, int>
{
    Task<KampDonemiYonetimBaglamDto> GetYonetimBaglamAsync(CancellationToken cancellationToken = default);

    Task<List<KampDonemiTesisAtamaDto>> GetTesisAtamalariAsync(int kampDonemiId, CancellationToken cancellationToken = default);

    Task<List<KampDonemiTesisAtamaDto>> KaydetTesisAtamalariAsync(int kampDonemiId, IReadOnlyCollection<KampDonemiTesisAtamaKayitDto> kayitlar, CancellationToken cancellationToken = default);
}
