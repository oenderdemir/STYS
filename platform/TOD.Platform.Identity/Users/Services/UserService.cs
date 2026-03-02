using AutoMapper;
using Microsoft.EntityFrameworkCore;
using TOD.Platform.Identity.Common.Enums;
using TOD.Platform.Identity.UserGroups.Repositories;
using TOD.Platform.Identity.UserUserGroups.Entities;
using TOD.Platform.Identity.Users.DTO;
using TOD.Platform.Identity.Users.Entities;
using TOD.Platform.Identity.Users.Repositories;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.Security.Auth.Services;

namespace TOD.Platform.Identity.Users.Services;

public class UserService : BaseRdbmsService<UserDto, User>, IUserService
{
    private const string DefaultPassword = "1";

    private readonly IUserGroupRepository _userGroupRepository;
    private readonly IPasswordHasher _passwordHasher;

    public UserService(IUserRepository userRepository, IUserGroupRepository userGroupRepository, IPasswordHasher passwordHasher, IMapper mapper)
        : base(userRepository, mapper)
    {
        _userGroupRepository = userGroupRepository;
        _passwordHasher = passwordHasher;
    }

    public override async Task<UserDto> AddAsync(UserDto dto)
    {
        var user = Mapper.Map<User>(dto);
        user.Status = ParseStatus(dto.Status);
        user.PasswordHash = _passwordHasher.Hash(DefaultPassword);
        user.UserUserGroups = new List<UserUserGroup>();

        foreach (var groupId in dto.UserGroups.Select(x => x.Id).Where(x => x.HasValue).Select(x => x!.Value).Distinct())
        {
            var userGroup = await _userGroupRepository.GetByIdAsync(groupId);
            if (userGroup is null)
            {
                continue;
            }

            user.UserUserGroups.Add(new UserUserGroup
            {
                User = user,
                UserGroup = userGroup
            });
        }

        await Repository.AddAsync(user);
        await Repository.SaveChangesAsync();

        return Mapper.Map<UserDto>(user);
    }

    public override async Task<UserDto> UpdateAsync(UserDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new InvalidOperationException("Id cannot be empty.");
        }

        var user = await Repository.GetByIdAsync(dto.Id.Value, q => q.IgnoreQueryFilters().Include(x => x.UserUserGroups));
        if (user is null)
        {
            throw new InvalidOperationException("User was not found.");
        }

        user.UserName = dto.UserName;
        user.NationalId = dto.NationalId;
        user.FirstName = dto.FirstName;
        user.LastName = dto.LastName;
        user.Email = dto.Email;
        user.AvatarPath = dto.AvatarPath;
        user.Status = ParseStatus(dto.Status);

        var desiredGroupIds = dto.UserGroups
            .Select(x => x.Id)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToHashSet();

        var existingGroupIds = user.UserUserGroups.Select(x => x.UserGroupId).ToHashSet();

        foreach (var userUserGroup in user.UserUserGroups)
        {
            userUserGroup.IsDeleted = !desiredGroupIds.Contains(userUserGroup.UserGroupId);
        }

        foreach (var groupId in desiredGroupIds.Except(existingGroupIds))
        {
            var userGroup = await _userGroupRepository.GetByIdAsync(groupId);
            if (userGroup is null)
            {
                continue;
            }

            user.UserUserGroups.Add(new UserUserGroup
            {
                UserId = user.Id,
                UserGroupId = groupId,
                User = user,
                UserGroup = userGroup,
                IsDeleted = false
            });
        }

        Repository.Update(user);
        await Repository.SaveChangesAsync();

        return dto;
    }

    public async Task ResetPasswordAsync(Guid id, UserResetPasswordDto dto)
    {
        if (dto is null)
        {
            throw new InvalidOperationException("Password payload cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(dto.NewPassword) || string.IsNullOrWhiteSpace(dto.NewPassword2))
        {
            throw new InvalidOperationException("New password is required.");
        }

        if (!string.Equals(dto.NewPassword, dto.NewPassword2, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("New passwords do not match.");
        }

        var user = await Repository.GetByIdAsync(id, q => q.IgnoreQueryFilters());
        if (user is null)
        {
            throw new InvalidOperationException("User was not found.");
        }

        user.PasswordHash = _passwordHasher.Hash(dto.NewPassword);
        user.Status = UserStatus.MustChangePassword;

        Repository.Update(user);
        await Repository.SaveChangesAsync();
    }

    private static UserStatus ParseStatus(string? rawStatus)
    {
        if (string.IsNullOrWhiteSpace(rawStatus))
        {
            return UserStatus.MustChangePassword;
        }

        return Enum.TryParse<UserStatus>(rawStatus, true, out var parsedStatus)
            ? parsedStatus
            : UserStatus.MustChangePassword;
    }
}
