using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Identity;
using TOD.Platform.Identity.Infrastructure.EntityFramework;
using TOD.Platform.Identity.UserKurums.Dto;
using TOD.Platform.Identity.UserKurums.Services;
using TOD.Platform.Identity.Users.DTO;
using TOD.Platform.Identity.Users.Entities;
using TOD.Platform.Identity.Users.Services;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace TOD.Platform.Identity.Users.Controllers;

public class UserController : UIController
{
    // Kullanıcı yönetimi hiyerarşi sıralaması
    private const int RankSuperAdmin = 100;
    private const int RankKurumAdmin = 80;
    private const int RankTesisYonetici = 60;
    private const int RankDiger = 20;

    // Role domain/name sabitleri (StructurePermissions ile uyumlu)
    private const string KullaniciTipiDomain = "KullaniciTipi";
    private const string AdminRoleName = "Admin";
    private const string KullaniciAtamaDomain = "KullaniciAtama";
    private const string TesisYoneticisiAtanabilirRoleName = "TesisYoneticisiAtanabilir";

    private readonly IUserService _userService;
    private readonly IUserKurumService _userKurumService;
    private readonly TodIdentityDbContext _dbContext;
    private readonly ICurrentTenantAccessor _currentTenantAccessor;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IKurumLookupService _kurumLookupService;

    public UserController(
        IUserService userService,
        IUserKurumService userKurumService,
        TodIdentityDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        ICurrentUserAccessor currentUserAccessor,
        IKurumLookupService kurumLookupService)
    {
        _userService = userService;
        _userKurumService = userKurumService;
        _dbContext = dbContext;
        _currentTenantAccessor = currentTenantAccessor;
        _currentUserAccessor = currentUserAccessor;
        _kurumLookupService = kurumLookupService;
    }

    [HttpGet]
    [Permission(IdentityPermissions.UserManagement.View, TodPlatformAuthorizationConstants.SuperAdminPermission)]
    public async Task<IEnumerable<UserDto>> GetAll(CancellationToken cancellationToken)
    {
        var include = BuildUserInclude();

        if (_currentTenantAccessor.IsSuperAdmin())
        {
            return await _userService.GetAllAsync(include);
        }

        var currentKurumId = _currentTenantAccessor.GetCurrentKurumId();
        if (!currentKurumId.HasValue)
        {
            return [];
        }

        var currentUserId = _currentUserAccessor.GetCurrentUserId();
        var currentRank = await GetCurrentUserRankAsync(cancellationToken);

        if (currentRank <= RankDiger)
        {
            return [];
        }

        // Temel kural: aynı kuruma ait, aktif, silinmemiş kullanıcılar
        // + Hiyerarşide üstteki kullanıcıları hariç tut
        // + Kendini hariç tut

        if (currentRank == RankKurumAdmin)
        {
            // Kurum Admin: kurum içindeki alt kullanıcıları görür
            // SuperAdmin ve diğer KurumAdmin'leri göremez
            return await _userService.WhereAsync(x =>
                    x.Id != currentUserId
                    && x.UserKurums.Any(uk => uk.KurumId == currentKurumId.Value && uk.AktifMi && !uk.IsDeleted)
                    // SuperAdmin'leri hariç tut
                    && !x.UserUserGroups.Any(uug => !uug.IsDeleted
                        && uug.UserGroup.UserGroupRoles.Any(ugr => !ugr.IsDeleted
                            && ugr.Role.Domain == KullaniciTipiDomain
                            && ugr.Role.Name == AdminRoleName))
                    // Diğer KurumAdmin'leri hariç tut
                    && !x.UserKurums.Any(uk => uk.KurumId == currentKurumId.Value
                        && uk.IsKurumAdmin && uk.AktifMi && !uk.IsDeleted),
                include);
        }

        if (currentRank == RankTesisYonetici)
        {
            // Tesis Yöneticisi: sadece alt rolleri görür (resepsiyonist, bina/restoran yöneticisi, garson)
            // SuperAdmin, KurumAdmin ve diğer TesisYöneticilerini göremez
            return await _userService.WhereAsync(x =>
                    x.Id != currentUserId
                    && x.UserKurums.Any(uk => uk.KurumId == currentKurumId.Value && uk.AktifMi && !uk.IsDeleted)
                    // SuperAdmin'leri hariç tut
                    && !x.UserUserGroups.Any(uug => !uug.IsDeleted
                        && uug.UserGroup.UserGroupRoles.Any(ugr => !ugr.IsDeleted
                            && ugr.Role.Domain == KullaniciTipiDomain
                            && ugr.Role.Name == AdminRoleName))
                    // KurumAdmin'leri hariç tut
                    && !x.UserKurums.Any(uk => uk.KurumId == currentKurumId.Value
                        && uk.IsKurumAdmin && uk.AktifMi && !uk.IsDeleted)
                    // Diğer TesisYöneticilerini hariç tut
                    && !x.UserUserGroups.Any(uug => !uug.IsDeleted
                        && uug.UserGroup.UserGroupRoles.Any(ugr => !ugr.IsDeleted
                            && ugr.Role.Domain == KullaniciAtamaDomain
                            && ugr.Role.Name == TesisYoneticisiAtanabilirRoleName)),
                include);
        }

        return [];
    }

    [HttpGet("{id:guid}")]
    [Permission(IdentityPermissions.UserManagement.View, TodPlatformAuthorizationConstants.SuperAdminPermission)]
    public async Task<ActionResult<UserDto?>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var include = BuildUserInclude();

        if (_currentTenantAccessor.IsSuperAdmin())
        {
            return Ok(await _userService.GetByIdAsync(id, include));
        }

        var currentKurumId = _currentTenantAccessor.GetCurrentKurumId();
        if (!currentKurumId.HasValue)
        {
            return Ok(null);
        }

        var canManage = await CanManageUserAsync(id, currentKurumId.Value, cancellationToken);
        if (!canManage)
        {
            return Ok(null);
        }

        var users = await _userService.WhereAsync(x =>
                x.Id == id && x.UserKurums.Any(uk => uk.KurumId == currentKurumId.Value && uk.AktifMi && !uk.IsDeleted),
            include);

        return Ok(users.FirstOrDefault());
    }

    [HttpPost]
    [Permission(IdentityPermissions.UserManagement.Manage, TodPlatformAuthorizationConstants.SuperAdminPermission)]
    public async Task<ActionResult<UserDto>> Create([FromBody] UserDto dto, CancellationToken cancellationToken)
    {
        await EnsureRequestedGroupsAreAllowedAsync(dto, cancellationToken);

        var resolvedKurumId = await ResolveCreateKurumIdAsync(dto.KurumId, cancellationToken);

        if (!resolvedKurumId.HasValue || resolvedKurumId.Value <= 0)
        {
            throw new BaseException("Kullanici olusturulacak kurum bilgisi cozumlenemedi.", 400);
        }

        if (dto.IsKurumAdmin && !_currentTenantAccessor.IsSuperAdmin())
        {
            throw new BaseException("Kurum admini olusturma yetkiniz bulunmuyor.", 403);
        }

        // Sadece SuperAdmin IsKurumAdmin=true atayabilir
        var isKurumAdmin = _currentTenantAccessor.IsSuperAdmin() && dto.IsKurumAdmin;

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var created = await _userService.AddAsync(dto);

        if (!created.Id.HasValue || created.Id.Value == Guid.Empty)
        {
            throw new BaseException("Kullanici olusturulamadi.", 500);
        }

        await _userKurumService.AssignAsync(new AssignUserKurumRequest
        {
            UserId = created.Id.Value,
            KurumId = resolvedKurumId.Value,
            VarsayilanMi = true,
            AktifMi = true,
            IsKurumAdmin = isKurumAdmin
        }, cancellationToken);

        created.KurumId = resolvedKurumId.Value;
        created.IsKurumAdmin = isKurumAdmin;

        await transaction.CommitAsync(cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Permission(IdentityPermissions.UserManagement.Manage, TodPlatformAuthorizationConstants.SuperAdminPermission)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UserDto dto, CancellationToken cancellationToken)
    {
        await EnsureCanManageUserAsync(id, cancellationToken);
        await EnsureRequestedGroupsAreAllowedAsync(dto, cancellationToken);
        dto.Id = id;
        await _userService.UpdateAsync(dto);
        return Ok();
    }

    [HttpDelete("{id:guid}")]
    [Permission(IdentityPermissions.UserManagement.Manage, TodPlatformAuthorizationConstants.SuperAdminPermission)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await EnsureCanManageUserAsync(id, cancellationToken);
        await _userService.DeleteAsync(id);
        return Ok();
    }

    [HttpPut("{id:guid}/password")]
    [Permission(IdentityPermissions.UserManagement.Manage, TodPlatformAuthorizationConstants.SuperAdminPermission)]
    public async Task<IActionResult> ResetPassword(Guid id, [FromBody] UserResetPasswordDto dto, CancellationToken cancellationToken)
    {
        await EnsureCanManageUserAsync(id, cancellationToken);
        await _userService.ResetPasswordAsync(id, dto);
        return Ok();
    }

    /// <summary>
    /// Mevcut kullanıcının hedef kullanıcıyı yönetip yönetemeyeceğini doğrular.
    /// Yönetemiyorsa 403 fırlatır.
    /// </summary>
    private async Task EnsureCanManageUserAsync(Guid targetUserId, CancellationToken cancellationToken)
    {
        if (_currentTenantAccessor.IsSuperAdmin())
        {
            return;
        }

        var currentKurumId = _currentTenantAccessor.GetCurrentKurumId();
        if (!currentKurumId.HasValue)
        {
            throw new BaseException("Bu kullaniciya erisim yetkiniz bulunmuyor.", 403);
        }

        var canManage = await CanManageUserAsync(targetUserId, currentKurumId.Value, cancellationToken);
        if (!canManage)
        {
            throw new BaseException("Bu kullaniciya erisim yetkiniz bulunmuyor.", 403);
        }
    }

    /// <summary>
    /// Mevcut kullanıcının hedef kullanıcıyı yönetip yönetemeyeceğini kontrol eder.
    /// Kural: Mevcut kullanıcı hedef kullanıcıdan hiyerarşik olarak üstte olmak zorunda.
    /// </summary>
    private async Task<bool> CanManageUserAsync(Guid targetUserId, int currentKurumId, CancellationToken cancellationToken)
    {
        // Kullanıcı kendini yönetemez
        var currentUserId = _currentUserAccessor.GetCurrentUserId();
        if (currentUserId.HasValue && currentUserId.Value == targetUserId)
        {
            return false;
        }

        // Hedef kullanıcı aynı kurumda mı?
        var targetInKurum = await _dbContext.UserKurums.AnyAsync(
            x => x.UserId == targetUserId && x.KurumId == currentKurumId && x.AktifMi && !x.IsDeleted,
            cancellationToken);

        if (!targetInKurum)
        {
            return false;
        }

        var currentRank = await GetCurrentUserRankAsync(cancellationToken);
        var targetRank = await GetUserRankAsync(targetUserId, currentKurumId, cancellationToken);

        // Hedef kullanıcı hiyerarşide eşit veya üstteyse yönetilemez
        return currentRank > targetRank;
    }

    /// <summary>
    /// Mevcut kullanıcının hiyerarşi sıralamasını döner.
    /// SuperAdmin=100, KurumAdmin=80, TesisYonetici=60, Diğer=20
    /// </summary>
    private async Task<int> GetCurrentUserRankAsync(CancellationToken cancellationToken)
    {
        if (_currentTenantAccessor.IsSuperAdmin()) return RankSuperAdmin;
        if (_currentTenantAccessor.IsKurumAdmin()) return RankKurumAdmin;

        var currentUserId = _currentUserAccessor.GetCurrentUserId();
        if (!currentUserId.HasValue) return 0;

        var isTesisYonetici = await _dbContext.UserUserGroups
            .Where(x => x.UserId == currentUserId.Value && !x.IsDeleted)
            .AnyAsync(x => x.UserGroup.UserGroupRoles.Any(ugr =>
                !ugr.IsDeleted
                && ugr.Role.Domain == KullaniciAtamaDomain
                && ugr.Role.Name == TesisYoneticisiAtanabilirRoleName),
                cancellationToken);

        return isTesisYonetici ? RankTesisYonetici : RankDiger;
    }

    /// <summary>
    /// Hedef kullanıcının hiyerarşi sıralamasını döner.
    /// SuperAdmin=100, KurumAdmin=80, TesisYonetici=60, Diğer=20
    /// </summary>
    private async Task<int> GetUserRankAsync(Guid userId, int kurumId, CancellationToken cancellationToken)
    {
        // SuperAdmin kontrolü (KullaniciTipi.Admin rolü)
        var isSuperAdmin = await _dbContext.UserUserGroups
            .Where(x => x.UserId == userId && !x.IsDeleted)
            .AnyAsync(x => x.UserGroup.UserGroupRoles.Any(ugr =>
                !ugr.IsDeleted
                && ugr.Role.Domain == KullaniciTipiDomain
                && ugr.Role.Name == AdminRoleName),
                cancellationToken);

        if (isSuperAdmin) return RankSuperAdmin;

        // KurumAdmin kontrolü (UserKurum.IsKurumAdmin)
        var isKurumAdmin = await _dbContext.UserKurums.AnyAsync(
            x => x.UserId == userId && x.KurumId == kurumId && x.IsKurumAdmin && x.AktifMi && !x.IsDeleted,
            cancellationToken);

        if (isKurumAdmin) return RankKurumAdmin;

        // TesisYonetici kontrolü (KullaniciAtama.TesisYoneticisiAtanabilir rolü)
        var isTesisYonetici = await _dbContext.UserUserGroups
            .Where(x => x.UserId == userId && !x.IsDeleted)
            .AnyAsync(x => x.UserGroup.UserGroupRoles.Any(ugr =>
                !ugr.IsDeleted
                && ugr.Role.Domain == KullaniciAtamaDomain
                && ugr.Role.Name == TesisYoneticisiAtanabilirRoleName),
                cancellationToken);

        return isTesisYonetici ? RankTesisYonetici : RankDiger;
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

    private Func<IQueryable<User>, IQueryable<User>> BuildUserInclude()
    {
        return q => q
            .Include(x => x.UserKurums.Where(x => !x.IsDeleted))
            .Include(x => x.UserUserGroups.Where(x => !x.IsDeleted))
                .ThenInclude(x => x.UserGroup)
                .ThenInclude(x => x.UserGroupRoles)
                .ThenInclude(x => x.Role);
    }

    private async Task EnsureRequestedGroupsAreAllowedAsync(UserDto dto, CancellationToken cancellationToken)
    {
        var requestedGroupIds = dto.UserGroups
            .Select(x => x.Id)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToList();

        if (requestedGroupIds.Count == 0)
        {
            return;
        }

        var requestedGroups = await _dbContext.UserGroups
            .Where(x => requestedGroupIds.Contains(x.Id) && !x.IsDeleted)
            .Select(x => new { x.Name })
            .ToListAsync(cancellationToken);

        if (!_currentTenantAccessor.IsSuperAdmin() &&
            requestedGroups.Any(x => string.Equals(x.Name, IdentityGroupNames.AdminGroup, StringComparison.OrdinalIgnoreCase)))
        {
            throw new BaseException("Yönetici Grubu atanamaz.", 403);
        }
    }
}
