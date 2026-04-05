using STYS.Kamp.Dto;

namespace STYS.Kamp.Services;

public interface IKampTahsisService
{
    Task<KampTahsisBaglamDto> GetBaglamAsync(CancellationToken cancellationToken = default);

    Task<List<KampTahsisListeDto>> GetListeAsync(KampTahsisFilterDto filter, CancellationToken cancellationToken = default);

    Task KararVerAsync(int kampBasvuruId, KampTahsisKararRequestDto request, CancellationToken cancellationToken = default);

    Task<KampTahsisOtomatikKararSonucDto> OtomatikKararUygulaAsync(KampTahsisOtomatikKararRequestDto request, CancellationToken cancellationToken = default);

    Task<KampNoShowIptalSonucDto> NoShowIptalUygulaAsync(int kampDonemiId, CancellationToken cancellationToken = default);
}
