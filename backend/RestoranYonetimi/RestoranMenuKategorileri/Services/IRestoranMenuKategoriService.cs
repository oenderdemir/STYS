using STYS.RestoranMenuKategorileri.Dtos;
using STYS.RestoranMenuKategorileri.Entities;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.RestoranMenuKategorileri.Services;

public interface IRestoranMenuKategoriService : IBaseRdbmsService<RestoranMenuKategoriDto, RestoranMenuKategori, int>
{
    Task<RestoranMenuDto> GetMenuByRestoranIdAsync(int restoranId, CancellationToken cancellationToken = default);
    Task<List<RestoranGlobalMenuKategoriDto>> GetGlobalListAsync(CancellationToken cancellationToken = default);
    Task<RestoranGlobalMenuKategoriDto> CreateGlobalAsync(CreateRestoranGlobalMenuKategoriRequest request, CancellationToken cancellationToken = default);
    Task<RestoranGlobalMenuKategoriDto> UpdateGlobalAsync(int id, UpdateRestoranGlobalMenuKategoriRequest request, CancellationToken cancellationToken = default);
    Task DeleteGlobalAsync(int id, CancellationToken cancellationToken = default);
    Task<RestoranKategoriAtamaBaglamDto> GetAtamaBaglamAsync(int restoranId, CancellationToken cancellationToken = default);
    Task<RestoranKategoriAtamaBaglamDto> SaveAtamalarAsync(SaveRestoranKategoriAtamaRequest request, CancellationToken cancellationToken = default);
}
