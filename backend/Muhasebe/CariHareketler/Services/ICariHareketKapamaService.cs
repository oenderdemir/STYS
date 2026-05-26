using STYS.Muhasebe.CariHareketler.Dtos;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.Muhasebe.CariHareketler.Services;

public interface ICariHareketKapamaService
{
    Task<CariHareketDto?> TahsilatOdemeIcinCariHareketOlusturVeKapatAsync(
        int tahsilatOdemeBelgesiId,
        CancellationToken cancellationToken = default);

    Task GeriAlAsync(
        int tahsilatOdemeBelgesiId,
        CancellationToken cancellationToken = default);
}
