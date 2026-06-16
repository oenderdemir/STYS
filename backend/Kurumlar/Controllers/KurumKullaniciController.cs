using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.Identity;
using TOD.Platform.Identity.Infrastructure.EntityFramework;
using TOD.Platform.Identity.UserKurums.Dto;
using TOD.Platform.Identity.UserKurums.Services;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Kurumlar.Controllers;

[Authorize(Policy = TodPlatformAuthorizationConstants.UiPolicy)]
[ApiController]
[Route("ui/kurum-kullanicilari")]
public class KurumKullaniciController : ControllerBase
{
    private readonly IUserKurumService _userKurumService;
    private readonly TodIdentityDbContext _identityDbContext;
    private readonly StysAppDbContext _stysDbContext;
    private readonly ICurrentTenantAccessor _currentTenantAccessor;

    public KurumKullaniciController(
        IUserKurumService userKurumService,
        TodIdentityDbContext identityDbContext,
        StysAppDbContext stysDbContext,
        ICurrentTenantAccessor currentTenantAccessor)
    {
        _userKurumService = userKurumService;
        _identityDbContext = identityDbContext;
        _stysDbContext = stysDbContext;
        _currentTenantAccessor = currentTenantAccessor;
    }

    [HttpGet("by-user/{userId:guid}")]
    [Permission(IdentityPermissions.UserManagement.Manage)]
    public async Task<ActionResult<List<UserKurumDto>>> GetByUser(Guid userId, CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
        {
            throw new BaseException("UserId zorunludur.", 400);
        }

        await EnsureManageScopeAsync();

        var items = await _userKurumService.GetByUserIdAsync(userId, cancellationToken);
        return Ok(FilterByEffectiveKurumScope(items));
    }

    [HttpGet("by-kurum/{kurumId:int}")]
    [Permission(IdentityPermissions.UserManagement.Manage)]
    public async Task<ActionResult<List<UserKurumDto>>> GetByKurum(int kurumId, CancellationToken cancellationToken)
    {
        await EnsureManageScopeAsync(kurumId);

        var items = await _userKurumService.GetByKurumIdAsync(kurumId, cancellationToken);
        return Ok(FilterByEffectiveKurumScope(items, kurumId));
    }

    [HttpPost("assign")]
    [Permission(IdentityPermissions.UserManagement.Manage)]
    public async Task<ActionResult<UserKurumDto>> Assign([FromBody] AssignUserKurumRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new BaseException("Istek bos olamaz.", 400);
        }

        await EnsureUserExistsAsync(request.UserId, cancellationToken);
        await EnsureKurumExistsAsync(request.KurumId, cancellationToken);
        await EnsureManageScopeAsync(request.KurumId);

        var created = await _userKurumService.AssignAsync(request, cancellationToken);
        return Ok(created);
    }

    [HttpPut("{id:guid}")]
    [Permission(IdentityPermissions.UserManagement.Manage)]
    public async Task<ActionResult<UserKurumDto>> Update(Guid id, [FromBody] UpdateUserKurumRequest request, CancellationToken cancellationToken)
    {
        var existing = await _userKurumService.GetByIdAsync(id);
        if (existing is null)
        {
            throw new BaseException("UserKurum kaydi bulunamadi.", 404);
        }

        await EnsureManageScopeAsync(existing.KurumId);

        var updated = await _userKurumService.UpdateAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    [Permission(IdentityPermissions.UserManagement.Manage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var existing = await _userKurumService.GetByIdAsync(id);
        if (existing is null)
        {
            throw new BaseException("UserKurum kaydi bulunamadi.", 404);
        }

        await EnsureManageScopeAsync(existing.KurumId);

        await _userKurumService.DeleteAsync(id, cancellationToken);
        return Ok();
    }

    private async Task EnsureUserExistsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var exists = await _identityDbContext.Users.AnyAsync(x => x.Id == userId, cancellationToken);
        if (!exists)
        {
            throw new BaseException("Kullanici bulunamadi.", 404);
        }
    }

    private async Task EnsureKurumExistsAsync(int kurumId, CancellationToken cancellationToken)
    {
        var exists = await _stysDbContext.Kurumlar.AnyAsync(x => x.Id == kurumId && x.AktifMi, cancellationToken);
        if (!exists)
        {
            throw new BaseException("Kurum bulunamadi veya aktif degil.", 404);
        }
    }

    private Task EnsureManageScopeAsync(int? targetKurumId = null)
    {
        if (_currentTenantAccessor.IsSuperAdmin())
        {
            return Task.CompletedTask;
        }

        var currentKurumId = _currentTenantAccessor.GetCurrentKurumId();
        if (!currentKurumId.HasValue || !_currentTenantAccessor.IsKurumAdmin())
        {
            throw new BaseException("Bu kurum icin yetkiniz bulunmuyor.", 403);
        }

        if (targetKurumId.HasValue && targetKurumId.Value != currentKurumId.Value)
        {
            throw new BaseException("Bu kurum icin yetkiniz bulunmuyor.", 403);
        }

        return Task.CompletedTask;
    }

    private List<UserKurumDto> FilterByEffectiveKurumScope(IEnumerable<UserKurumDto> items, int? targetKurumId = null)
    {
        if (_currentTenantAccessor.IsSuperAdmin())
        {
            return items
                .OrderByDescending(x => x.VarsayilanMi)
                .ThenBy(x => x.KurumId)
                .ThenBy(x => x.UserId)
                .ToList();
        }

        var currentKurumId = _currentTenantAccessor.GetCurrentKurumId();
        if (!currentKurumId.HasValue)
        {
            return [];
        }

        var kurumId = targetKurumId ?? currentKurumId.Value;
        return items
            .Where(x => x.KurumId == kurumId)
            .OrderByDescending(x => x.VarsayilanMi)
            .ThenBy(x => x.UserId)
            .ToList();
    }
}
