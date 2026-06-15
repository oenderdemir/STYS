using STYS.Kurumlar.Dto;
using STYS.Kurumlar.Entities;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.Kurumlar.Services;

public interface IKurumService : IBaseRdbmsService<KurumDto, Kurum, int>
{
    Task<List<KurumDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<KurumDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<KurumDto> CreateAsync(CreateKurumRequest request, CancellationToken cancellationToken = default);

    Task<KurumDto> UpdateAsync(int id, UpdateKurumRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
