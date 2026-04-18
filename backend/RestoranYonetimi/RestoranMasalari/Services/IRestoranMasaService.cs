using STYS.RestoranMasalari.Dtos;
using STYS.RestoranMasalari.Entities;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.RestoranMasalari.Services;

public interface IRestoranMasaService : IBaseRdbmsService<RestoranMasaDto, RestoranMasa, int>
{
    Task<List<RestoranMasaDto>> GetByRestoranIdAsync(int restoranId, CancellationToken cancellationToken = default);
}
