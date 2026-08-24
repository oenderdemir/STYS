using STYS.Muhasebe.SarfFisleri.Dtos;
using STYS.Muhasebe.SarfFisleri.Entities;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.Muhasebe.SarfFisleri.Services;

public interface ISarfFisiService : IBaseRdbmsService<SarfFisiDto, SarfFisi, int>
{
    Task<SarfFisiDto> UpdateSatirlarAsync(int id, UpdateSarfFisiSatirlarRequest request, CancellationToken cancellationToken = default);
    Task<SarfFisiDto> AddSatirAsync(int id, AddSarfFisiSatirRequest request, CancellationToken cancellationToken = default);
    Task DeleteSatirAsync(int id, int satirId, CancellationToken cancellationToken = default);
    Task<SarfFisiDto> KesinlestirAsync(int id, CancellationToken cancellationToken = default);
    Task<SarfFisiDto> IptalAsync(int id, string? iptalAciklamasi = null, CancellationToken cancellationToken = default);
    Task<List<SarfBirimSecenekDto>> GetBirimlerAsync(int tesisId, CancellationToken cancellationToken = default);
    Task<List<SarfOdaSecenekDto>> GetOdalarAsync(int tesisId, CancellationToken cancellationToken = default);
}
