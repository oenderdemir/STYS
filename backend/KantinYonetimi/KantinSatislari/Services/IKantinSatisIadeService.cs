using STYS.KantinYonetimi.KantinSatislari.Dtos;

namespace STYS.KantinYonetimi.KantinSatislari.Services;

public interface IKantinSatisIadeService
{
    Task<KantinSatisIadeDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<KantinSatisIadeDto> CreateAsync(CreateKantinSatisIadeRequest request, CancellationToken cancellationToken = default);
    Task<KantinSatisIadeDto> KesinlestirAsync(int id, CancellationToken cancellationToken = default);
    Task<List<KantinSatisIadeOzetDto>> GetSatisIadeOzetiAsync(int kantinSatisId, CancellationToken cancellationToken = default);
}
