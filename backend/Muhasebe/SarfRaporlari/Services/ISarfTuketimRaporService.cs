using STYS.Muhasebe.SarfRaporlari.Dtos;
using TOD.Platform.Persistence.Rdbms.Paging;

namespace STYS.Muhasebe.SarfRaporlari.Services;

public interface ISarfTuketimRaporService
{
    Task<PagedResult<SarfTuketimDetayRaporSatirDto>> GetDetayAsync(
        PagedRequest request,
        SarfTuketimRaporFilterDto filter,
        CancellationToken cancellationToken = default);

    Task<List<SarfTuketimDetayRaporSatirDto>> GetDetayListAsync(
        SarfTuketimRaporFilterDto filter,
        CancellationToken cancellationToken = default);

    Task<List<SarfTuketimMalzemeOzetDto>> GetMalzemeOzetAsync(
        SarfTuketimRaporFilterDto filter,
        CancellationToken cancellationToken = default);

    Task<List<SarfTuketimKullanimYeriOzetDto>> GetKullanimYeriOzetAsync(
        SarfTuketimRaporFilterDto filter,
        CancellationToken cancellationToken = default);
}
