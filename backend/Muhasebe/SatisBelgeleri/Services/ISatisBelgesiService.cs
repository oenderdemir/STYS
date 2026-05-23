using STYS.Muhasebe.SatisBelgeleri.Dtos;
using TOD.Platform.SharedKernel.Responses;

namespace STYS.Muhasebe.SatisBelgeleri.Services;

public interface ISatisBelgesiService
{
    Task<SatisBelgesiDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<List<SatisBelgesiDto>> FilterAsync(SatisBelgesiFilterDto filter, CancellationToken cancellationToken = default);
    Task<SatisBelgesiDto> CreateAsync(CreateSatisBelgesiRequest request, CancellationToken cancellationToken = default);
    Task<SatisBelgesiDto> UpdateAsync(int id, UpdateSatisBelgesiRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task MuhasebeOnayinaGonderAsync(int id, CancellationToken cancellationToken = default);
    Task MuhasebeOnaylaAsync(int id, CancellationToken cancellationToken = default);
    Task ReddetAsync(int id, string redNedeni, CancellationToken cancellationToken = default);
    Task IptalEtAsync(int id, CancellationToken cancellationToken = default);
}
