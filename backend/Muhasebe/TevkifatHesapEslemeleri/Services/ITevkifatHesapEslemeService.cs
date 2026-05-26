using STYS.Muhasebe.TevkifatHesapEslemeleri.Dtos;
using STYS.Muhasebe.TevkifatHesapEslemeleri.Entities;
using TOD.Platform.Persistence.Rdbms.Paging;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.Muhasebe.TevkifatHesapEslemeleri.Services;

public interface ITevkifatHesapEslemeService : IBaseRdbmsService<TevkifatHesapEslemeDto, TevkifatHesapEsleme, int>
{
    Task<IEnumerable<TevkifatHesapEslemeDto>> GetAllAsync(
        int? tesisId = null,
        string? islemYonu = null,
        bool? aktifMi = null,
        CancellationToken cancellationToken = default);

    Task<PagedResult<TevkifatHesapEslemeDto>> GetPagedAsync(
        PagedRequest request,
        int? tesisId = null,
        string? islemYonu = null,
        bool? aktifMi = null,
        CancellationToken cancellationToken = default);

    Task<TevkifatHesapEslemeDto?> GetAktifEslemeAsync(
        int? tesisId,
        string islemYonu,
        int tevkifatPay,
        int tevkifatPayda,
        CancellationToken cancellationToken = default);
}
