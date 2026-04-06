using STYS.Kamp.Dto;

namespace STYS.Kamp.Services;

public interface IKampBasvuruService
{
    Task<KampBasvuruBaglamDto> GetBaglamAsync(CancellationToken cancellationToken = default);
    Task<KampBasvuruOnizlemeDto> OnizleAsync(KampBasvuruRequestDto request, CancellationToken cancellationToken = default);
    Task<KampBasvuruDto> BasvuruOlusturAsync(KampBasvuruRequestDto request, CancellationToken cancellationToken = default);
    Task<KampBasvuruDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<KampBasvuruDto> GetByBasvuruNoAsync(string basvuruNo, CancellationToken cancellationToken = default);
    Task<List<KampBasvuruDto>> GetBenimBasvurularimAsync(CancellationToken cancellationToken = default);
    Task<KampKatilimciIptalSonucDto> KatilimciIptalEtAsync(int kampBasvuruId, int katilimciId, CancellationToken cancellationToken = default);
}
