using STYS.RestoranMenuUrunleri.Dtos;

namespace STYS.RestoranMenuUrunleri.Services;

public interface IRestoranMenuUrunService
{
    Task<List<RestoranMenuUrunDto>> GetListAsync(int? kategoriId, CancellationToken cancellationToken = default);
    Task<RestoranMenuUrunDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<RestoranMenuUrunDto> CreateAsync(CreateRestoranMenuUrunRequest request, CancellationToken cancellationToken = default);
    Task<RestoranMenuUrunDto> UpdateAsync(int id, UpdateRestoranMenuUrunRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
