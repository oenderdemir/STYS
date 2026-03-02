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
        var candidateUserIds = await GetGroupUserIdsByGroupTypePermissionAsync(
            nameof(StructurePermissions.KullaniciGrupTipi.TesisYoneticisi),
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
        var receptionistCandidateUserIds = await GetGroupUserIdsByGroupTypePermissionAsync(
            nameof(StructurePermissions.KullaniciGrupTipi.Resepsiyonist),
            cancellationToken);

        if (receptionistCandidateUserIds.Count == 0)
        {
            return [];
        }

        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        var receptionistUserIds = receptionistCandidateUserIds;

        if (scope.IsScoped)
        {
            var allReceptionistUserIds = receptionistCandidateUserIds;

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

            receptionistUserIds = allowedReceptionistUserIds;
        }

        var users = await _userRepository
            .Where(x => x.Status != UserStatus.Blocked)
            .Where(x => receptionistUserIds.Contains(x.Id))
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
}
