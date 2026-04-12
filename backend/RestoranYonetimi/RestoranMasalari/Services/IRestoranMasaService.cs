using STYS.RestoranMasalari.Dtos;

namespace STYS.RestoranMasalari.Services;

public interface IRestoranMasaService
{
    Task<List<RestoranMasaDto>> GetListAsync(int? restoranId, CancellationToken cancellationToken = default);
    Task<List<RestoranMasaDto>> GetByRestoranIdAsync(int restoranId, CancellationToken cancellationToken = default);
    Task<RestoranMasaDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<RestoranMasaDto> CreateAsync(CreateRestoranMasaRequest request, CancellationToken cancellationToken = default);
    Task<RestoranMasaDto> UpdateAsync(int id, UpdateRestoranMasaRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
