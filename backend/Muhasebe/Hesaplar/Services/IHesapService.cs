using STYS.Muhasebe.Hesaplar.Dtos;
using STYS.Muhasebe.Hesaplar.Entities;
using TOD.Platform.Persistence.Rdbms.Paging;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.Muhasebe.Hesaplar.Services;

public interface IHesapService : IBaseRdbmsService<HesapDto, Hesap, int>
{
    Task<List<HesapDto>> GetDetailedListAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<HesapDto>> GetPagedAsync(
        PagedRequest request,
        int? tesisId,
        CancellationToken cancellationToken = default);
    Task<HesapDto?> GetDetailByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<List<HesapLookupDto>> GetKasaHesapLookupsAsync(int? tesisId, CancellationToken cancellationToken = default);
    Task<List<HesapLookupDto>> GetBankaHesapLookupsAsync(int? tesisId, CancellationToken cancellationToken = default);
    Task<List<HesapLookupDto>> GetDepoLookupsAsync(int? tesisId, CancellationToken cancellationToken = default);
    Task<List<HesapLookupDto>> GetMuhasebeKodLookupsAsync(string? startsWith, CancellationToken cancellationToken = default);
}
