using STYS.RestoranMenuKategorileri.Dtos;

namespace STYS.RestoranMenuKategorileri.Services;

public interface IRestoranMenuKategoriService
{
    Task<List<RestoranMenuKategoriDto>> GetListAsync(int? restoranId, CancellationToken cancellationToken = default);
    Task<RestoranMenuKategoriDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<RestoranMenuKategoriDto> CreateAsync(CreateRestoranMenuKategoriRequest request, CancellationToken cancellationToken = default);
    Task<RestoranMenuKategoriDto> UpdateAsync(int id, UpdateRestoranMenuKategoriRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<RestoranMenuDto> GetMenuByRestoranIdAsync(int restoranId, CancellationToken cancellationToken = default);
    Task<List<RestoranGlobalMenuKategoriDto>> GetGlobalListAsync(CancellationToken cancellationToken = default);
    Task<RestoranGlobalMenuKategoriDto> CreateGlobalAsync(CreateRestoranGlobalMenuKategoriRequest request, CancellationToken cancellationToken = default);
    Task<RestoranGlobalMenuKategoriDto> UpdateGlobalAsync(int id, UpdateRestoranGlobalMenuKategoriRequest request, CancellationToken cancellationToken = default);
    Task DeleteGlobalAsync(int id, CancellationToken cancellationToken = default);
    Task<RestoranKategoriAtamaBaglamDto> GetAtamaBaglamAsync(int restoranId, CancellationToken cancellationToken = default);
    Task<RestoranKategoriAtamaBaglamDto> SaveAtamalarAsync(SaveRestoranKategoriAtamaRequest request, CancellationToken cancellationToken = default);
}
