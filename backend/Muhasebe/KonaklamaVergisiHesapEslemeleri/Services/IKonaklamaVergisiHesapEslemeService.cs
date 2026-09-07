using STYS.Muhasebe.KonaklamaVergisiHesapEslemeleri.Dtos;
using TOD.Platform.Persistence.Rdbms.Paging;

namespace STYS.Muhasebe.KonaklamaVergisiHesapEslemeleri.Services;

public interface IKonaklamaVergisiHesapEslemeService
{
    Task<IEnumerable<KonaklamaVergisiHesapEslemeDto>> GetAllAsync(
        int? tesisId = null,
        bool? aktifMi = null,
        CancellationToken cancellationToken = default);

    Task<PagedResult<KonaklamaVergisiHesapEslemeDto>> GetPagedAsync(
        PagedRequest request,
        int? tesisId = null,
        bool? aktifMi = null,
        CancellationToken cancellationToken = default);

    Task<KonaklamaVergisiHesapEslemeDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<KonaklamaVergisiHesapEslemeDto?> GetAktifEslemeAsync(int? tesisId, CancellationToken cancellationToken = default);
    Task<int?> ResolveAktifHesapIdAsync(int tesisId, CancellationToken cancellationToken = default);
    Task<List<KonaklamaVergisiHesapSecenekDto>> GetHesapSecenekleriAsync(int? tesisId, CancellationToken cancellationToken = default);
    Task<KonaklamaVergisiHesapEslemeDto> AddAsync(KonaklamaVergisiHesapEslemeDto dto, CancellationToken cancellationToken = default);
    Task<KonaklamaVergisiHesapEslemeDto> UpdateAsync(KonaklamaVergisiHesapEslemeDto dto, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
