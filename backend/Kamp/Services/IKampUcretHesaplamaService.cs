using STYS.Kamp.Dto;
using STYS.Kamp.Entities;
using STYS.Tesisler.Entities;

namespace STYS.Kamp.Services;

public interface IKampUcretHesaplamaService
{
    Task HesaplaAsync(
        KampBasvuruRequestDto request,
        KampDonemi kampDonemi,
        Tesis tesis,
        KampBasvuruOnizlemeDto onizleme,
        CancellationToken cancellationToken = default);
}
