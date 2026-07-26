using STYS.Muhasebe.OdemeIzleme.Dtos;
using TOD.Platform.Persistence.Rdbms.Paging;

namespace STYS.Muhasebe.OdemeIzleme.Services;

/// <summary>
/// Odeme arastirmasini ODEME BELGESI MERKEZLI olmaktan cikaran, capraz-kaynak SALT-OKUNUR arama.
/// Ayni mali islem birden fazla kaynakta bulundugunda tekillestirilir; hicbir kayit
/// olusturulmaz/degistirilmez.
/// </summary>
public interface IOdemeCaprazAramaService
{
    Task<PagedResult<OdemeAdayiDto>> AraAsync(
        PagedRequest request, OdemeCaprazAramaFilterDto filter, CancellationToken cancellationToken = default);
}
