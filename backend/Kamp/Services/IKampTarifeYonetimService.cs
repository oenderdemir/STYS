using STYS.Kamp.Dto;

namespace STYS.Kamp.Services;

public interface IKampTarifeYonetimService
{
    Task<KampTarifeYonetimBaglamDto> GetBaglamAsync(CancellationToken cancellationToken = default);

    Task<List<KampKonaklamaTarifeYonetimDto>> GetTarifelerAsync(int kampProgramiId, CancellationToken cancellationToken = default);

    Task<List<KampKonaklamaTarifeYonetimDto>> KaydetAsync(int kampProgramiId, KampTarifeKaydetRequestDto request, CancellationToken cancellationToken = default);
}
