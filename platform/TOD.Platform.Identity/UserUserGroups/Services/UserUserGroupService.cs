using AutoMapper;
using Microsoft.EntityFrameworkCore;
using TOD.Platform.Identity.Security.Services;
using TOD.Platform.Identity.UserUserGroups.DTO;
using TOD.Platform.Identity.UserUserGroups.Entities;
using TOD.Platform.Identity.UserUserGroups.Repositories;
using TOD.Platform.Persistence.Rdbms.Services;

namespace TOD.Platform.Identity.UserUserGroups.Services;

public class UserUserGroupService : BaseRdbmsService<UserUserGroupDto, UserUserGroup>, IUserUserGroupService
{
    private readonly ITokenInvalidationService _tokenInvalidationService;

    public UserUserGroupService(
        IUserUserGroupRepository userUserGroupRepository,
        ITokenInvalidationService tokenInvalidationService,
        IMapper mapper)
        : base(userUserGroupRepository, mapper)
    {
        _tokenInvalidationService = tokenInvalidationService;
    }

    public override Task<IEnumerable<UserUserGroupDto>> GetAllAsync(Func<IQueryable<UserUserGroup>, IQueryable<UserUserGroup>>? include = null)
    {
        return base.GetAllAsync(q => q.Include(x => x.User).Include(x => x.UserGroup));
    }

    public override async Task<UserUserGroupDto> AddAsync(UserUserGroupDto dto)
    {
        var created = await base.AddAsync(dto);
        var userId = created.User?.Id ?? dto.User?.Id;

        if (userId.HasValue)
        {
            await _tokenInvalidationService.InvalidateUserAsync(userId.Value, "User group assignment added", CancellationToken.None);
        }

        return created;
    }

    public override async Task<UserUserGroupDto> UpdateAsync(UserUserGroupDto dto)
    {
        var affectedUserIds = new HashSet<Guid>();

        if (dto.Id.HasValue)
        {
            var existingEntity = await Repository.GetByIdAsync(dto.Id.Value);
            if (existingEntity is not null)
            {
                affectedUserIds.Add(existingEntity.UserId);
            }
        }

        if (dto.User?.Id.HasValue == true)
        {
            affectedUserIds.Add(dto.User.Id.Value);
        }

        var updated = await base.UpdateAsync(dto);
        if (updated.User?.Id.HasValue == true)
        {
            affectedUserIds.Add(updated.User.Id.Value);
        }

        await _tokenInvalidationService.InvalidateUsersAsync(affectedUserIds, "User group assignment updated", CancellationToken.None);
        return updated;
    }

    public override async Task DeleteAsync(Guid id)
    {
        var existingEntity = await Repository.GetByIdAsync(id);
        await base.DeleteAsync(id);

        if (existingEntity is not null)
        {
            await _tokenInvalidationService.InvalidateUserAsync(existingEntity.UserId, "User group assignment removed", CancellationToken.None);
        }
    }
}
