using STYS.Kamp.Dto;

namespace STYS.Kamp.Services;

public interface IKampPuanlamaService
{
    Task<KampBasvuruOnizlemeDto> PuanlaAsync(
        KampBasvuruRequestDto request,
        KampBasvuruOnizlemeDto onizleme,
        int kampProgramiId,
        IReadOnlyCollection<int> gecmisKatilimYillari,
        CancellationToken cancellationToken = default);
}
