using STYS.IsletmeAlanlari.Dto;
using STYS.IsletmeAlanlari.Entities;
using TOD.Platform.Persistence.Rdbms.Paging;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.IsletmeAlanlari.Services;

public interface IIsletmeAlaniService : IBaseRdbmsService<IsletmeAlaniDto, IsletmeAlani, int>
{
    Task<List<IsletmeAlaniSinifiDto>> GetSiniflarAsync(bool onlyActive, CancellationToken cancellationToken = default);
    Task<PagedResult<IsletmeAlaniSinifiDto>> GetSiniflarPagedAsync(PagedRequest request, string? query, CancellationToken cancellationToken = default);
    Task<IsletmeAlaniSinifiDto?> GetSinifByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IsletmeAlaniSinifiDto> AddSinifAsync(IsletmeAlaniSinifiDto dto, CancellationToken cancellationToken = default);
    Task<IsletmeAlaniSinifiDto> UpdateSinifAsync(IsletmeAlaniSinifiDto dto, CancellationToken cancellationToken = default);
    Task DeleteSinifAsync(int id, CancellationToken cancellationToken = default);
}
