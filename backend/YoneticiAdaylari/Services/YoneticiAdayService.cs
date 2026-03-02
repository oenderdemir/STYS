using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.YoneticiAdaylari.Dto;
using TOD.Platform.Identity.Infrastructure.EntityFramework;
using TOD.Platform.Identity.Common.Enums;
using TOD.Platform.Identity.Users.Repositories;

namespace STYS.YoneticiAdaylari.Services;

public class YoneticiAdayService : IYoneticiAdayService
{
    private const string TesisYoneticiGroupName = "TesisYoneticiGrubu";
    private const string BinaYoneticiGroupName = "BinaYoneticiGrubu";
    private const string ResepsiyonistGroupName = "ResepsiyonistGrubu";

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
        var query = _userRepository.Where(x => x.Status != UserStatus.Blocked);

        if (scope.IsScoped)
        {
            var visibleUserIds = await GetScopedUserIdsAsync(scope, cancellationToken);
            query = query.Where(x => visibleUserIds.Contains(x.Id));
        }

        var users = await query
            .OrderBy(x => x.UserName)
            .Select(x => new YoneticiAdayDto
            {
                Id = x.Id,
                UserName = x.UserName,
                AdSoyad = string.Join(' ', new[] { x.FirstName, x.LastName }.Where(part => !string.IsNullOrWhiteSpace(part)))
            })
            .ToListAsync(cancellationToken);

        return users;
    }

    public async Task<List<YoneticiAdayDto>> GetTesisYoneticiAdaylariAsync(CancellationToken cancellationToken = default)
    {
        var tesisYoneticiGroupId = await _identityDbContext.UserGroups
            .Where(x => x.Name == TesisYoneticiGroupName)
            .Select(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (tesisYoneticiGroupId == Guid.Empty)
        {
            return [];
        }

        var candidateUserIds = await _identityDbContext.UserUserGroups
            .Where(x => x.UserGroupId == tesisYoneticiGroupId)
            .Select(x => x.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

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

        var users = await _userRepository
            .Where(x => x.Status != UserStatus.Blocked)
            .Where(x => candidateUserIds.Contains(x.Id))
            .OrderBy(x => x.UserName)
            .Select(x => new YoneticiAdayDto
            {
                Id = x.Id,
                UserName = x.UserName,
                AdSoyad = string.Join(' ', new[] { x.FirstName, x.LastName }.Where(part => !string.IsNullOrWhiteSpace(part)))
            })
            .ToListAsync(cancellationToken);

        return users;
    }

    public async Task<List<YoneticiAdayDto>> GetResepsiyonistAdaylariAsync(CancellationToken cancellationToken = default)
    {
        var receptionistGroupId = await _identityDbContext.UserGroups
            .Where(x => x.Name == ResepsiyonistGroupName)
            .Select(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (receptionistGroupId == Guid.Empty)
        {
            return [];
        }

        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        var receptionistUserIdsQuery = _identityDbContext.UserUserGroups
            .Where(x => x.UserGroupId == receptionistGroupId)
            .Select(x => x.UserId)
            .Distinct();

        if (scope.IsScoped)
        {
            var allReceptionistUserIds = await _identityDbContext.UserUserGroups
                .Where(x => x.UserGroupId == receptionistGroupId)
                .Select(x => x.UserId)
                .Distinct()
                .ToListAsync(cancellationToken);

            var scopedReceptionistUserIds = await _stysDbContext.TesisResepsiyonistleri
                .Where(x => scope.TesisIds.Contains(x.TesisId))
                .Select(x => x.UserId)
                .Distinct()
                .ToListAsync(cancellationToken);

            var ownerAllowedReceptionistUserIds = await GetOwnerAllowedUserIdsAsync(
                allReceptionistUserIds,
                scope,
                cancellationToken);

            var allowedReceptionistUserIds = scopedReceptionistUserIds
                .Concat(ownerAllowedReceptionistUserIds)
                .Distinct()
                .ToList();

            receptionistUserIdsQuery = receptionistUserIdsQuery.Where(x => allowedReceptionistUserIds.Contains(x));
        }

        var users = await _userRepository
            .Where(x => x.Status != UserStatus.Blocked)
            .Where(x => receptionistUserIdsQuery.Contains(x.Id))
            .OrderBy(x => x.UserName)
            .Select(x => new YoneticiAdayDto
            {
                Id = x.Id,
                UserName = x.UserName,
                AdSoyad = string.Join(' ', new[] { x.FirstName, x.LastName }.Where(part => !string.IsNullOrWhiteSpace(part)))
            })
            .ToListAsync(cancellationToken);

        return users;
    }

    private async Task<HashSet<Guid>> GetScopedUserIdsAsync(DomainAccessScope scope, CancellationToken cancellationToken)
    {
        var allTesisManagerUserIds = await GetGroupUserIdsAsync(TesisYoneticiGroupName, cancellationToken);
        var allBinaManagerUserIds = await GetGroupUserIdsAsync(BinaYoneticiGroupName, cancellationToken);
        var allReceptionistUserIds = await GetGroupUserIdsAsync(ResepsiyonistGroupName, cancellationToken);

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

    private async Task<List<Guid>> GetGroupUserIdsAsync(string groupName, CancellationToken cancellationToken)
    {
        var groupId = await _identityDbContext.UserGroups
            .Where(x => x.Name == groupName)
            .Select(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (groupId == Guid.Empty)
        {
            return [];
        }

        return await _identityDbContext.UserUserGroups
            .Where(x => x.UserGroupId == groupId)
            .Select(x => x.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    private async Task<List<Guid>> GetOwnerAllowedUserIdsAsync(
        IReadOnlyCollection<Guid> userIds,
        DomainAccessScope scope,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return [];
        }

        var ownerRows = await _stysDbContext.KullaniciTesisSahiplikleri
            .Where(x => userIds.Contains(x.UserId))
            .Select(x => new
            {
                x.UserId,
                x.TesisId
            })
            .ToListAsync(cancellationToken);

        var ownerByUserId = ownerRows
            .GroupBy(x => x.UserId)
            .ToDictionary(
                x => x.Key,
                x => x.Select(row => row.TesisId).FirstOrDefault());

        return userIds
            .Where(userId =>
            {
                if (!ownerByUserId.TryGetValue(userId, out var ownerTesisId))
                {
                    return true;
                }

                if (!ownerTesisId.HasValue)
                {
                    return true;
                }

                return scope.TesisIds.Contains(ownerTesisId.Value);
            })
            .ToList();
    }
}
