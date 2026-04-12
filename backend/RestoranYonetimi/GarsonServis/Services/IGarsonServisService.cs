using STYS.GarsonServis.Dtos;

namespace STYS.GarsonServis.Services;

public interface IGarsonServisService
{
    Task<List<GarsonMasaDto>> GetMasalarAsync(int restoranId, CancellationToken cancellationToken = default);
    Task<MasaOturumuDto?> GetMasaOturumuByMasaAsync(int masaId, CancellationToken cancellationToken = default);
    Task<MasaOturumuDto> StartOrGetMasaOturumuAsync(int masaId, CreateMasaOturumuRequest request, CancellationToken cancellationToken = default);
    Task<MasaOturumuDto> AddKalemAsync(int oturumId, AddMasaOturumuKalemiRequest request, CancellationToken cancellationToken = default);
    Task<MasaOturumuDto> UpdateKalemAsync(int oturumId, int kalemId, UpdateMasaOturumuKalemiRequest request, CancellationToken cancellationToken = default);
    Task<MasaOturumuDto> DeleteKalemAsync(int oturumId, int kalemId, CancellationToken cancellationToken = default);
    Task<MasaOturumuDto> UpdateNotAsync(int oturumId, UpdateMasaOturumuNotRequest request, CancellationToken cancellationToken = default);
    Task<MasaOturumuDto> UpdateDurumAsync(int oturumId, UpdateMasaOturumuDurumRequest request, CancellationToken cancellationToken = default);
    Task<GarsonMenuDto> GetMenuAsync(int restoranId, CancellationToken cancellationToken = default);
}
