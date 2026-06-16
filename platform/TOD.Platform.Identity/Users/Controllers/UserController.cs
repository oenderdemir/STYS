using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Identity;
using TOD.Platform.Identity.Infrastructure.EntityFramework;
using TOD.Platform.Identity.UserKurums.Dto;
using TOD.Platform.Identity.UserKurums.Services;
using TOD.Platform.Identity.Users.DTO;
using TOD.Platform.Identity.Users.Services;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace TOD.Platform.Identity.Users.Controllers;

public class UserController : UIController
{
    private readonly IUserService _userService;
    private readonly IUserKurumService _userKurumService;
    private readonly TodIdentityDbContext _dbContext;
    private readonly ICurrentTenantAccessor _currentTenantAccessor;
    private readonly IKurumLookupService _kurumLookupService;

    public UserController(
        IUserService userService,
        IUserKurumService userKurumService,
        TodIdentityDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        IKurumLookupService kurumLookupService)
    {
        _userService = userService;
        _userKurumService = userKurumService;
        _dbContext = dbContext;
        _currentTenantAccessor = currentTenantAccessor;
        _kurumLookupService = kurumLookupService;
    }

    [HttpGet]
    [Permission(IdentityPermissions.UserManagement.View, TodPlatformAuthorizationConstants.SuperAdminPermission)]
    public Task<IEnumerable<UserDto>> GetAll()
    {
        return _userService.GetAllAsync(q => q
            .Include(x => x.UserUserGroups.Where(x => !x.IsDeleted))
            .ThenInclude(x => x.UserGroup)
            .ThenInclude(x => x.UserGroupRoles)
            .ThenInclude(x => x.Role));
    }

    [HttpGet("{id:guid}")]
    [Permission(IdentityPermissions.UserManagement.View, TodPlatformAuthorizationConstants.SuperAdminPermission)]
    public Task<UserDto?> GetById(Guid id)
    {
        return _userService.GetByIdAsync(id, q => q
            .Include(x => x.UserUserGroups.Where(x => !x.IsDeleted))
            .ThenInclude(x => x.UserGroup)
            .ThenInclude(x => x.UserGroupRoles)
            .ThenInclude(x => x.Role));
    }

    [HttpPost]
    [Permission(IdentityPermissions.UserManagement.Manage, TodPlatformAuthorizationConstants.SuperAdminPermission)]
    public async Task<ActionResult<UserDto>> Create([FromBody] UserDto dto, CancellationToken cancellationToken)
    {
        var resolvedKurumId = await ResolveCreateKurumIdAsync(dto.KurumId, cancellationToken);
        if (dto.IsKurumAdmin && !_currentTenantAccessor.IsSuperAdmin())
        {
            throw new BaseException("Kurum admini olusturma yetkiniz bulunmuyor.", 403);
        }

        var isKurumAdmin = dto.IsKurumAdmin;

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var created = await _userService.AddAsync(dto);
        if (resolvedKurumId.HasValue)
        {
            var assignment = new AssignUserKurumRequest
            {
                UserId = created.Id ?? Guid.Empty,
                KurumId = resolvedKurumId.Value,
                VarsayilanMi = true,
                AktifMi = true,
                IsKurumAdmin = isKurumAdmin
            };

            if (assignment.UserId == Guid.Empty)
            {
                throw new BaseException("Kullanici olusturulamadi.", 500);
            }

            await _userKurumService.AssignAsync(assignment, cancellationToken);
            created.KurumId = resolvedKurumId;
            created.IsKurumAdmin = isKurumAdmin;
        }

        await transaction.CommitAsync(cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Permission(IdentityPermissions.UserManagement.Manage, TodPlatformAuthorizationConstants.SuperAdminPermission)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UserDto dto)
    {
        dto.Id = id;
        await _userService.UpdateAsync(dto);
        return Ok();
    }

    [HttpDelete("{id:guid}")]
    [Permission(IdentityPermissions.UserManagement.Manage, TodPlatformAuthorizationConstants.SuperAdminPermission)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _userService.DeleteAsync(id);
        return Ok();
    }

    [HttpPut("{id:guid}/password")]
    [Permission(IdentityPermissions.UserManagement.Manage, TodPlatformAuthorizationConstants.SuperAdminPermission)]
    public async Task<IActionResult> ResetPassword(Guid id, [FromBody] UserResetPasswordDto dto)
    {
        await _userService.ResetPasswordAsync(id, dto);
        return Ok();
    }

    private async Task<int?> ResolveCreateKurumIdAsync(int? requestedKurumId, CancellationToken cancellationToken)
    {
        if (_currentTenantAccessor.IsSuperAdmin())
        {
            if (!requestedKurumId.HasValue || requestedKurumId.Value <= 0)
            {
                throw new BaseException("KurumId zorunludur.", 400);
            }

            await EnsureKurumExistsAsync(requestedKurumId.Value, cancellationToken);
            return requestedKurumId.Value;
        }

        if (!_currentTenantAccessor.IsKurumAdmin())
        {
            throw new BaseException("Kullanıcının kurum erişimi bulunmuyor.", 401);
        }

        var activeKurumId = _currentTenantAccessor.GetCurrentKurumId();
        if (!activeKurumId.HasValue)
        {
            throw new BaseException("Kullanıcının aktif kurum bilgisi bulunmuyor.", 400);
        }

        if (requestedKurumId.HasValue && requestedKurumId.Value != activeKurumId.Value)
        {
            throw new BaseException("Bu kurum icin yetkiniz bulunmuyor.", 403);
        }

        await EnsureKurumExistsAsync(activeKurumId.Value, cancellationToken);
        return activeKurumId.Value;
    }

    private async Task EnsureKurumExistsAsync(int kurumId, CancellationToken cancellationToken)
    {
        var exists = await _kurumLookupService.IsActiveKurumAsync(kurumId, cancellationToken);
        if (!exists)
        {
            throw new BaseException("Kurum bulunamadi veya aktif degil.", 404);
        }
    }
}
