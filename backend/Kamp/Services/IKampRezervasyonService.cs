using STYS.Kamp.Dto;

namespace STYS.Kamp.Services;

public interface IKampRezervasyonService
{
    Task<KampRezervasyonBaglamDto> GetBaglamAsync(CancellationToken cancellationToken = default);
    Task<List<KampRezervasyonListeDto>> GetListeAsync(KampRezervasyonFilterDto filter, CancellationToken cancellationToken = default);
    Task<KampRezervasyonUretSonucDto> UretAsync(int kampBasvuruId, CancellationToken cancellationToken = default);
    Task IptalEtAsync(int id, KampRezervasyonIptalRequestDto request, CancellationToken cancellationToken = default);
}
