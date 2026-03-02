using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.YoneticiAdaylari.Dto;
using TOD.Platform.Identity.Common.Enums;
using TOD.Platform.Identity.Infrastructure.EntityFramework;
using TOD.Platform.Identity.Users.Repositories;

namespace STYS.YoneticiAdaylari.Services;

public class YoneticiAdayService : IYoneticiAdayService
{
    private readonly IUserRepository _userRepository;
    private readonly StysAppDbContext _stysDbContext;
    private readonly TodIdentityDbContext _identityDbContext;
    private readonly IUserAccessScopeService _userAccessScopeService;

    public YoneticiAdayService(
        IUserRepository userRepository,
        StysAppDbContext stysDbContext,
        TodIdentityDbContext identityDbContext,
        IUserAccessScopeService userAccessScopeService)
    {
        _userRepository = userRepository;
        _stysDbContext = stysDbContext;
        _identityDbContext = identityDbContext;
        _userAccessScopeService = userAccessScopeService;
    }

    public async Task<List<YoneticiAdayDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        if (!scope.IsScoped)
        {
            return await QueryUsersAsCandidateDto(_userRepository.Where(x => x.Status != UserStatus.Blocked), cancellationToken);
        }

        var visibleUserIds = await GetScopedUserIdsAsync(scope, cancellationToken);
        if (visibleUserIds.Count == 0)
        {
            return [];
        }

        var scopedQuery = _userRepository
            .Where(x => x.Status != UserStatus.Blocked)
            .Where(x => visibleUserIds.Contains(x.Id));

        return await QueryUsersAsCandidateDto(scopedQuery, cancellationToken);
    }

    public async Task<List<YoneticiAdayDto>> GetTesisYoneticiAdaylariAsync(CancellationToken cancellationToken = default)
    {
        var candidateUserIds = await GetUserIdsByTargetGroupMarkerAsync(
            StructurePermissions.KullaniciAtama.TesisYoneticisiAtanabilir,
            cancellationToken);

        if (candidateUserIds.Count == 0)
        {
            return [];
        }

        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        if (scope.IsScoped)
        {
            var visibleUserIds = await GetScopedUserIdsAsync(scope, cancellationToken);
            candidateUserIds = candidateUserIds
                .Where(visibleUserIds.Contains)
                .ToList();
        }

        if (candidateUserIds.Count == 0)
        {
            return [];
        }

        var query = _userRepository
            .Where(x => x.Status != UserStatus.Blocked)
            .Where(x => candidateUserIds.Contains(x.Id));

        return await QueryUsersAsCandidateDto(query, cancellationToken);
    }

    public async Task<List<YoneticiAdayDto>> GetResepsiyonistAdaylariAsync(CancellationToken cancellationToken = default)
    {
        var receptionistCandidateUserIds = await GetUserIdsByTargetGroupMarkerAsync(
            StructurePermissions.KullaniciAtama.ResepsiyonistAtanabilir,
            cancellationToken);

        if (receptionistCandidateUserIds.Count == 0)
        {
            return [];
        }

        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        var receptionistUserIds = receptionistCandidateUserIds;

        if (scope.IsScoped)
        {
            var scopedReceptionistUserIds = await _stysDbContext.TesisResepsiyonistleri
                .Where(x => scope.TesisIds.Contains(x.TesisId))
                .Select(x => x.UserId)
                .Distinct()
                .ToListAsync(cancellationToken);

            var ownerAllowedReceptionistUserIds = await GetOwnerAllowedUserIdsAsync(
                receptionistCandidateUserIds,
                scope,
                cancellationToken);

            receptionistUserIds = scopedReceptionistUserIds
                .Concat(ownerAllowedReceptionistUserIds)
                .Distinct()
                .ToList();
        }

        if (receptionistUserIds.Count == 0)
        {
            return [];
        }

        var query = _userRepository
            .Where(x => x.Status != UserStatus.Blocked)
            .Where(x => receptionistUserIds.Contains(x.Id));

        return await QueryUsersAsCandidateDto(query, cancellationToken);
    }

    private async Task<HashSet<Guid>> GetScopedUserIdsAsync(DomainAccessScope scope, CancellationToken cancellationToken)
    {
        var allTesisManagerUserIds = await GetUserIdsByTargetGroupMarkerAsync(
            StructurePermissions.KullaniciAtama.TesisYoneticisiAtanabilir,
            cancellationToken);
        var allBinaManagerUserIds = await GetUserIdsByTargetGroupMarkerAsync(
            StructurePermissions.KullaniciAtama.BinaYoneticisiAtanabilir,
            cancellationToken);
        var allReceptionistUserIds = await GetUserIdsByTargetGroupMarkerAsync(
            StructurePermissions.KullaniciAtama.ResepsiyonistAtanabilir,
            cancellationToken);

        var tesisManagerUserIds = await _stysDbContext.TesisYoneticileri
            .Where(x => scope.TesisIds.Contains(x.TesisId))
            .Select(x => x.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var receptionistUserIds = await _stysDbContext.TesisResepsiyonistleri
            .Where(x => scope.TesisIds.Contains(x.TesisId))
            .Select(x => x.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var binaManagerUserIds = await _stysDbContext.BinaYoneticileri
            .Where(x => scope.BinaIds.Contains(x.BinaId))
            .Select(x => x.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var ownerAllowedTesisManagerUserIds = await GetOwnerAllowedUserIdsAsync(
            allTesisManagerUserIds,
            scope,
            cancellationToken);

        var ownerAllowedBinaManagerUserIds = await GetOwnerAllowedUserIdsAsync(
            allBinaManagerUserIds,
            scope,
            cancellationToken);

        var ownerAllowedReceptionistUserIds = await GetOwnerAllowedUserIdsAsync(
            allReceptionistUserIds,
            scope,
            cancellationToken);

        return tesisManagerUserIds
            .Concat(ownerAllowedTesisManagerUserIds)
            .Concat(receptionistUserIds)
            .Concat(ownerAllowedReceptionistUserIds)
            .Concat(binaManagerUserIds)
            .Concat(ownerAllowedBinaManagerUserIds)
            .ToHashSet();
    }

    private async Task<List<Guid>> GetOwnerAllowedUserIdsAsync(
        IReadOnlyCollection<Guid> userIds,
        DomainAccessScope scope,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0 || scope.TesisIds.Count == 0)
        {
            return [];
        }

        return await _stysDbContext.KullaniciTesisSahiplikleri
            .Where(x => x.TesisId.HasValue)
            .Where(x => scope.TesisIds.Contains(x.TesisId!.Value))
            .Where(x => userIds.Contains(x.UserId))
            .Select(x => x.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    private async Task<List<Guid>> GetUserIdsByTargetGroupMarkerAsync(
        string targetGroupMarkerPermission,
        CancellationToken cancellationToken)
    {
        var markerRoleName = GetRoleName(targetGroupMarkerPermission);

        return await _identityDbContext.UserUserGroups
            .Where(x => x.UserGroup.UserGroupRoles.Any(ugr =>
                ugr.Role.Domain == nameof(StructurePermissions.KullaniciAtama)
                && ugr.Role.Name == markerRoleName))
            .Select(x => x.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    private static Task<List<YoneticiAdayDto>> QueryUsersAsCandidateDto(
        IQueryable<TOD.Platform.Identity.Users.Entities.User> query,
        CancellationToken cancellationToken)
    {
        return query
            .OrderBy(x => x.UserName)
            .Select(x => new YoneticiAdayDto
            {
                Id = x.Id,
                UserName = x.UserName,
                AdSoyad = string.Join(' ', new[] { x.FirstName, x.LastName }.Where(part => !string.IsNullOrWhiteSpace(part)))
            })
            .ToListAsync(cancellationToken);
    }

    private static string GetRoleName(string permission)
    {
        var splitIndex = permission.LastIndexOf('.');
        return splitIndex < 0 ? permission : permission[(splitIndex + 1)..];
    }

}
