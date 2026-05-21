using STYS.Muhasebe.DonemKapanis.Dtos;

namespace STYS.Muhasebe.DonemKapanis.Services;

public interface IDonemKapanisKontrolService
{
    Task<DonemKapanisKontrolDto> KontrolEtAsync(
        DonemKapanisKontrolFilterDto filter,
        CancellationToken cancellationToken = default);
}
