using STYS.Muhasebe.StokCikis.Dtos;

namespace STYS.Muhasebe.StokCikis.Services;

public interface IStokCikisStrategy
{
    string Yontem { get; }

    Task<StokCikisSonuc> BaslatAsync(StokCikisIstegi istek, CancellationToken cancellationToken = default);
}
