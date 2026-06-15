using TOD.Platform.Identity.UserKurums.Dto;
using TOD.Platform.Identity.UserKurums.Entities;
using TOD.Platform.Persistence.Rdbms.Services;

namespace TOD.Platform.Identity.UserKurums.Services;

public interface IUserKurumService : IBaseRdbmsService<UserKurumDto, UserKurum, Guid>
{
    Task<List<UserKurumDto>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<List<UserKurumDto>> GetByKurumIdAsync(int kurumId, CancellationToken cancellationToken = default);

    Task<UserKurumDto> AssignAsync(AssignUserKurumRequest request, CancellationToken cancellationToken = default);

    Task<UserKurumDto> UpdateAsync(Guid id, UpdateUserKurumRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
