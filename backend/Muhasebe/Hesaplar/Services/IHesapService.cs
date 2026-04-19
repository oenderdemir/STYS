using STYS.Muhasebe.Hesaplar.Dtos;
using STYS.Muhasebe.Hesaplar.Entities;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.Muhasebe.Hesaplar.Services;

public interface IHesapService : IBaseRdbmsService<HesapDto, Hesap, int>
{
    Task<List<HesapDto>> GetDetailedListAsync(CancellationToken cancellationToken = default);
    Task<HesapDto?> GetDetailByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<List<HesapLookupDto>> GetKasaHesapLookupsAsync(CancellationToken cancellationToken = default);
    Task<List<HesapLookupDto>> GetBankaHesapLookupsAsync(CancellationToken cancellationToken = default);
    Task<List<HesapLookupDto>> GetDepoLookupsAsync(CancellationToken cancellationToken = default);
    Task<List<HesapLookupDto>> GetMuhasebeKodLookupsAsync(string? startsWith, CancellationToken cancellationToken = default);
}
