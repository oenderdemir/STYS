using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using STYS.Kurumlar.Dto;
using STYS.Kurumlar.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Identity;
using TOD.Platform.Identity.Infrastructure.EntityFramework;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;
using STYS.Infrastructure.EntityFramework;

namespace STYS.Kurumlar.Controllers;

public class KurumController : UIController
{
    private readonly IKurumService _kurumService;
    private readonly TodIdentityDbContext _identityDbContext;
    private readonly StysAppDbContext _stysDbContext;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly ICurrentTenantAccessor _currentTenantAccessor;

    public KurumController(
        IKurumService kurumService,
        TodIdentityDbContext identityDbContext,
        StysAppDbContext stysDbContext,
        ICurrentUserAccessor currentUserAccessor,
        ICurrentTenantAccessor currentTenantAccessor)
    {
        _kurumService = kurumService;
        _identityDbContext = identityDbContext;
        _stysDbContext = stysDbContext;
        _currentUserAccessor = currentUserAccessor;
        _currentTenantAccessor = currentTenantAccessor;
    }

    // Kurum yaratma ve silme SuperAdmin ile sinirli. Guncelleme aktif kurum scope'una gore yapilir.

    [HttpGet]
    [Permission(IdentityPermissions.UserManagement.View, TodPlatformAuthorizationConstants.SuperAdminPermission)]
    public Task<List<KurumDto>> GetAll(CancellationToken cancellationToken)
    {
        return GetAccessibleKurumlarAsync(cancellationToken);
    }

    [HttpGet("{id:int}")]
    [Permission(IdentityPermissions.UserManagement.View, TodPlatformAuthorizationConstants.SuperAdminPermission)]
    public async Task<ActionResult<KurumDto>> GetById(int id, CancellationToken cancellationToken)
    {
        await EnsureCanAccessKurumAsync(id, cancellationToken);
        var kurum = await _kurumService.GetByIdAsync(id, cancellationToken);
        if (kurum is null)
        {
            return NotFound();
        }

        return Ok(kurum);
    }

    [HttpPost]
    [Permission(TodPlatformAuthorizationConstants.SuperAdminPermission)]
    public async Task<ActionResult<KurumDto>> Create([FromBody] CreateKurumRequest request, CancellationToken cancellationToken)
    {
        var created = await _kurumService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Permission(IdentityPermissions.UserManagement.Manage, TodPlatformAuthorizationConstants.SuperAdminPermission)]
    public async Task<ActionResult<KurumDto>> Update(int id, [FromBody] UpdateKurumRequest request, CancellationToken cancellationToken)
    {
        await EnsureCanUpdateKurumAsync(id, cancellationToken);
        var updated = await _kurumService.UpdateAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    [HttpDelete("{id:int}")]
    [Permission(TodPlatformAuthorizationConstants.SuperAdminPermission)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _kurumService.DeleteAsync(id, cancellationToken);
        return Ok();
    }

    [HttpGet("benim-kurumlarim")]
    [Permission]
    public async Task<ActionResult<List<KurumDto>>> GetMyKurumlar(CancellationToken cancellationToken)
    {
        var kurumlar = await GetAccessibleKurumlarAsync(cancellationToken);
        return Ok(kurumlar);
    }

    private async Task<List<KurumDto>> GetAccessibleKurumlarAsync(CancellationToken cancellationToken)
    {
        var kurumlar = await _kurumService.GetAllAsync(cancellationToken);
        if (_currentTenantAccessor.IsSuperAdmin())
        {
            return kurumlar.Where(x => x.AktifMi).ToList();
        }

        var accessibleIds = (await GetAccessibleKurumIdsAsync(cancellationToken)).ToHashSet();
        if (accessibleIds.Count == 0)
        {
            return [];
        }

        return kurumlar
            .Where(x => x.AktifMi && x.Id.HasValue && accessibleIds.Contains(x.Id.Value))
            .ToList();
    }

    private async Task<List<int>> GetAccessibleKurumIdsAsync(CancellationToken cancellationToken)
    {
        if (_currentTenantAccessor.IsSuperAdmin())
        {
            var kurumlar = await _kurumService.GetAllAsync(cancellationToken);
            return kurumlar
                .Where(x => x.Id.HasValue)
                .Select(x => x.Id!.Value)
                .ToList();
        }

        var currentUserId = _currentUserAccessor.GetCurrentUserId();
        if (!currentUserId.HasValue)
        {
            return [];
        }

        return await _identityDbContext.UserKurums
            .Where(x => x.UserId == currentUserId.Value && x.AktifMi)
            .Select(x => x.KurumId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    private async Task EnsureCanAccessKurumAsync(int targetKurumId, CancellationToken cancellationToken)
    {
        var accessibleIds = (await GetAccessibleKurumIdsAsync(cancellationToken)).ToHashSet();
        if (accessibleIds.Count == 0 || !accessibleIds.Contains(targetKurumId))
        {
            throw new BaseException("Bu kurum icin yetkiniz bulunmuyor.", 403);
        }
    }

    private async Task EnsureCanUpdateKurumAsync(int targetKurumId, CancellationToken cancellationToken)
    {
        if (_currentTenantAccessor.IsSuperAdmin())
        {
            return;
        }

        var currentKurumId = _currentTenantAccessor.GetCurrentKurumId();
        if (!currentKurumId.HasValue || !_currentTenantAccessor.IsKurumAdmin() || currentKurumId.Value != targetKurumId)
        {
            throw new BaseException("Bu kurum icin yetkiniz bulunmuyor.", 403);
        }

        var exists = await _stysDbContext.Kurumlar.AnyAsync(x => x.Id == targetKurumId && x.AktifMi, cancellationToken);
        if (!exists)
        {
            throw new BaseException("Kurum bulunamadi veya aktif degil.", 404);
        }
    }
}
