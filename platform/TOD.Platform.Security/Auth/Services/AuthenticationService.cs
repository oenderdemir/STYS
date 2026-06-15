using TOD.Platform.Security.Auth.DTO;
using TOD.Platform.Security.Auth.Models;
using TOD.Platform.Security.Auth.Options;
using Microsoft.Extensions.Options;
using TOD.Platform.SharedKernel.Exceptions;

namespace TOD.Platform.Security.Auth.Services;

public class AuthenticationService<TKey> : IAuthenticationService<TKey> where TKey : struct
{
    private readonly IIdentityStore<TKey> _identityStore;
    private readonly IJwtTokenService _tokenService;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly ICurrentTenantAccessor _currentTenantAccessor;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IOptions<JwtTokenOptions> _jwtTokenOptions;

    public AuthenticationService(
        IIdentityStore<TKey> identityStore,
        IJwtTokenService tokenService,
        ICurrentUserAccessor currentUserAccessor,
        ICurrentTenantAccessor currentTenantAccessor,
        IPasswordHasher passwordHasher,
        IOptions<JwtTokenOptions> jwtTokenOptions)
    {
        _identityStore = identityStore;
        _tokenService = tokenService;
        _currentUserAccessor = currentUserAccessor;
        _currentTenantAccessor = currentTenantAccessor;
        _passwordHasher = passwordHasher;
        _jwtTokenOptions = jwtTokenOptions;
    }

    public async Task<LoginResponseDto> ChangePassword(ChangePasswordRequestDto model, CancellationToken cancellationToken = default)
    {
        if (model.NewPassword != model.NewPassword2)
        {
            throw new InvalidOperationException("New passwords do not match.");
        }

        var userName = _currentUserAccessor.GetCurrentUserName();
        if (string.IsNullOrWhiteSpace(userName))
        {
            throw new UnauthorizedAccessException("Current user is not authenticated.");
        }

        var user = await _identityStore.FindByUserNameAsync(userName, cancellationToken);
        if (user is null)
        {
            throw new UnauthorizedAccessException("User not found.");
        }

        if (!_passwordHasher.Verify(model.CurrentPassword, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Current password is invalid.");
        }

        var newPasswordHash = _passwordHasher.Hash(model.NewPassword);
        await _identityStore.UpdatePasswordHashAsync(user.Id, newPasswordHash, cancellationToken);

        return await LogoutAsync(cancellationToken);
    }

    public async Task<LoginResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
        {
            throw new ArgumentException("Username and password are required.", nameof(request));
        }

        var user = await _identityStore.FindByUserNameAsync(request.UserName, cancellationToken);
        if (user is null)
        {
            return CreateUnauthorizedResponse();
        }

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            return CreateUnauthorizedResponse();
        }

        var normalizedPermissions = await GetValidatedPermissionsAsync(user.Id, cancellationToken);

        return await CreateAuthenticatedResponseAsync(
            user,
            normalizedPermissions,
            issueNewRefreshToken: true,
            existingRefreshToken: null,
            existingRefreshTokenExpireDate: null,
            cancellationToken);
    }

    public async Task<LoginResponseDto> RefreshAsync(RefreshTokenRequestDto request, CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return CreateUnauthorizedResponse();
        }

        var now = DateTime.UtcNow;
        var rotatedRefreshToken = await _identityStore.RotateRefreshTokenAsync(
            request.RefreshToken,
            now,
            now.AddDays(_jwtTokenOptions.Value.RefreshTokenExpirationDays),
            cancellationToken);

        if (rotatedRefreshToken.Status != RefreshTokenRotationStatus.Rotated)
        {
            return CreateUnauthorizedResponse();
        }

        var user = await _identityStore.FindByIdAsync(rotatedRefreshToken.UserId, cancellationToken);
        if (user is null)
        {
            return CreateUnauthorizedResponse();
        }

        var normalizedPermissions = await GetValidatedPermissionsAsync(user.Id, cancellationToken);

        return await CreateAuthenticatedResponseAsync(
            user,
            normalizedPermissions,
            issueNewRefreshToken: false,
            existingRefreshToken: rotatedRefreshToken.RefreshToken,
            existingRefreshTokenExpireDate: rotatedRefreshToken.ExpiresAt,
            cancellationToken,
            request.KurumId);
    }

    public async Task<LoginResponseDto> LogoutAsync(CancellationToken cancellationToken = default)
    {
        var currentUserId = _currentUserAccessor.GetCurrentUserId();
        if (typeof(TKey) == typeof(Guid) && currentUserId.HasValue)
        {
            await _identityStore.RevokeAllRefreshTokensAsync(
                (TKey)(object)currentUserId.Value,
                DateTime.UtcNow,
                "User logout",
                cancellationToken);
        }

        return CreateUnauthorizedResponse();
    }

    public async Task<CurrentUserResponseDto> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        var currentUserId = _currentUserAccessor.GetCurrentUserId();
        if (!currentUserId.HasValue)
        {
            throw new UnauthorizedAccessException("Current user is not authenticated.");
        }

        var user = await _identityStore.FindByIdAsync((TKey)(object)currentUserId.Value, cancellationToken);
        if (user is null)
        {
            throw new UnauthorizedAccessException("User not found.");
        }

        var defaultRoute = await _identityStore.GetDefaultRouteAsync((TKey)(object)currentUserId.Value, cancellationToken);
        var kurumIds = (await _identityStore.GetUserKurumIdsAsync((TKey)(object)currentUserId.Value, cancellationToken)).Distinct().ToList();
        var kurumAdminKurumIds = (await _identityStore.GetKurumAdminKurumIdsAsync((TKey)(object)currentUserId.Value, cancellationToken)).Distinct().ToList();
        var aktifKurumId = _currentTenantAccessor.GetCurrentKurumId();
        var isSuperAdmin = HasSuperAdminPermission((await GetValidatedPermissionsAsync((TKey)(object)currentUserId.Value, cancellationToken)).ToList());
        var isKurumAdmin = aktifKurumId.HasValue && await _identityStore.UserIsKurumAdminAsync((TKey)(object)currentUserId.Value, aktifKurumId.Value, cancellationToken);

        return new CurrentUserResponseDto
        {
            UserName = user.UserName,
            UserStatus = user.Status,
            DefaultRoute = defaultRoute,
            AktifKurumId = aktifKurumId,
            KurumIds = kurumIds,
            KurumAdminKurumIds = kurumAdminKurumIds,
            IsKurumAdmin = isKurumAdmin,
            IsSuperAdmin = isSuperAdmin
        };
    }

    public async Task<LoginResponseDto> SelectKurumAsync(SelectKurumRequestDto request, CancellationToken cancellationToken = default)
    {
        if (request.KurumId <= 0)
        {
            throw new BaseException("KurumId zorunludur.", 400);
        }

        var currentUserId = _currentUserAccessor.GetCurrentUserId();
        if (!currentUserId.HasValue)
        {
            throw new UnauthorizedAccessException("Current user is not authenticated.");
        }

        var user = await _identityStore.FindByIdAsync((TKey)(object)currentUserId.Value, cancellationToken);
        if (user is null)
        {
            throw new UnauthorizedAccessException("User not found.");
        }

        var normalizedPermissions = await GetValidatedPermissionsAsync((TKey)(object)currentUserId.Value, cancellationToken);
        var isSuperAdmin = HasSuperAdminPermission(normalizedPermissions);
        var kurumIds = (await _identityStore.GetUserKurumIdsAsync((TKey)(object)currentUserId.Value, cancellationToken)).Distinct().ToList();

        if (!isSuperAdmin)
        {
            var hasAccess = await _identityStore.UserHasKurumAccessAsync((TKey)(object)currentUserId.Value, request.KurumId, cancellationToken);
            if (!hasAccess)
            {
                throw new BaseException("Bu kurum icin yetkiniz bulunmuyor.", 403);
            }
        }

        var kurumAdminKurumIds = (await _identityStore.GetKurumAdminKurumIdsAsync((TKey)(object)currentUserId.Value, cancellationToken)).Distinct().ToList();

        return await CreateAuthenticatedResponseAsync(
            user,
            normalizedPermissions,
            issueNewRefreshToken: false,
            existingRefreshToken: string.Empty,
            existingRefreshTokenExpireDate: null,
            cancellationToken,
            request.KurumId,
            kurumIdsOverride: kurumIds,
            kurumAdminKurumIdsOverride: kurumAdminKurumIds,
            isSuperAdminOverride: isSuperAdmin);
    }

    private async Task<List<string>> GetValidatedPermissionsAsync(TKey userId, CancellationToken cancellationToken)
    {
        var permissions = await _identityStore.GetPermissionsAsync(userId, cancellationToken);
        var normalizedPermissions = permissions
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var invalidPermissions = normalizedPermissions
            .Where(x => !IsDomainNamePermission(x))
            .ToList();

        if (invalidPermissions.Count > 0)
        {
            throw new InvalidOperationException(
                $"Invalid permission format. Permissions must be in 'Domain.Name' format. Invalid values: {string.Join(", ", invalidPermissions)}");
        }

        return normalizedPermissions;
    }

    private async Task<LoginResponseDto> CreateAuthenticatedResponseAsync(
        IdentityUser<TKey> user,
        List<string> normalizedPermissions,
        bool issueNewRefreshToken,
        string? existingRefreshToken,
        DateTime? existingRefreshTokenExpireDate,
        CancellationToken cancellationToken)
    {
        return await CreateAuthenticatedResponseAsync(
            user,
            normalizedPermissions,
            issueNewRefreshToken,
            existingRefreshToken,
            existingRefreshTokenExpireDate,
            cancellationToken,
            null);
    }

    private async Task<LoginResponseDto> CreateAuthenticatedResponseAsync(
        IdentityUser<TKey> user,
        List<string> normalizedPermissions,
        bool issueNewRefreshToken,
        string? existingRefreshToken,
        DateTime? existingRefreshTokenExpireDate,
        CancellationToken cancellationToken,
        int? requestedKurumId,
        IReadOnlyCollection<int>? kurumIdsOverride = null,
        IReadOnlyCollection<int>? kurumAdminKurumIdsOverride = null,
        bool? isSuperAdminOverride = null)
    {
        var kurumIds = kurumIdsOverride?.Distinct().ToList()
            ?? (await _identityStore.GetUserKurumIdsAsync(user.Id, cancellationToken)).Distinct().ToList();
        var kurumAdminKurumIds = kurumAdminKurumIdsOverride?.Distinct().ToList()
            ?? (await _identityStore.GetKurumAdminKurumIdsAsync(user.Id, cancellationToken)).Distinct().ToList();
        var isSuperAdmin = isSuperAdminOverride ?? HasSuperAdminPermission(normalizedPermissions);
        var defaultKurumId = await _identityStore.GetDefaultKurumIdAsync(user.Id, cancellationToken);
        var aktifKurumId = await ResolveActiveKurumIdAsync(
            user.Id,
            kurumIds,
            defaultKurumId,
            isSuperAdmin,
            requestedKurumId,
            cancellationToken);
        var isKurumAdmin = aktifKurumId.HasValue && await _identityStore.UserIsKurumAdminAsync(user.Id, aktifKurumId.Value, cancellationToken);

        var generatedToken = await _tokenService.GenerateToken(new GenerateTokenRequest
        {
            UserId = user.Id.ToString() ?? string.Empty,
            UserName = user.UserName,
            Name = user.Name ?? string.Empty,
            Surname = user.Surname ?? string.Empty,
            Email = user.Email ?? string.Empty,
            Permissions = normalizedPermissions,
            TokenVersion = user.TokenVersion,
            KurumId = aktifKurumId,
            KurumIds = kurumIds,
            IsKurumAdmin = isKurumAdmin,
            IsSuperAdmin = isSuperAdmin
        }, cancellationToken);

        var defaultRoute = await _identityStore.GetDefaultRouteAsync(user.Id, cancellationToken);

        string refreshToken;
        DateTime? refreshTokenExpireDate;

        if (issueNewRefreshToken)
        {
            var issuedRefreshToken = await _identityStore.IssueRefreshTokenAsync(
                user.Id,
                DateTime.UtcNow.AddDays(_jwtTokenOptions.Value.RefreshTokenExpirationDays),
                cancellationToken);

            refreshToken = issuedRefreshToken.RefreshToken;
            refreshTokenExpireDate = issuedRefreshToken.ExpiresAt;
        }
        else
        {
            refreshToken = existingRefreshToken ?? string.Empty;
            refreshTokenExpireDate = existingRefreshTokenExpireDate;
        }

        return new LoginResponseDto
        {
            AuthenticateResult = true,
            AuthToken = generatedToken.Token,
            AccessTokenExpireDate = generatedToken.TokenExpireDate,
            RefreshToken = refreshToken,
            RefreshTokenExpireDate = refreshTokenExpireDate,
            DefaultRoute = defaultRoute,
            UserStatus = user.Status,
            Permissions = normalizedPermissions.ToList(),
            AktifKurumId = aktifKurumId,
            KurumIds = kurumIds,
            KurumAdminKurumIds = kurumAdminKurumIds,
            IsKurumAdmin = isKurumAdmin,
            IsSuperAdmin = isSuperAdmin
        };
    }

    private static LoginResponseDto CreateUnauthorizedResponse()
    {
        return new LoginResponseDto
        {
            AuthenticateResult = false,
            AuthToken = string.Empty,
            AccessTokenExpireDate = DateTime.UtcNow,
            RefreshToken = string.Empty,
            RefreshTokenExpireDate = null,
            DefaultRoute = null,
            Permissions = []
        };
    }

    private static bool IsDomainNamePermission(string permission)
    {
        var firstDotIndex = permission.IndexOf('.');
        if (firstDotIndex <= 0 || firstDotIndex == permission.Length - 1)
        {
            return false;
        }

        return permission.IndexOf('.', firstDotIndex + 1) < 0;
    }

    private static bool HasSuperAdminPermission(IEnumerable<string> permissions)
    {
        // TODO Tenant Faz 5: SuperAdmin permission adi seed/role yapisiyla kesinlestirilecek.
        return permissions.Any(x =>
            string.Equals(x, "System.SuperAdmin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(x, "Admin.SuperAdmin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(x, "Platform.SuperAdmin", StringComparison.OrdinalIgnoreCase));
    }

    private async Task<int?> ResolveActiveKurumIdAsync(
        TKey userId,
        IReadOnlyCollection<int> kurumIds,
        int? defaultKurumId,
        bool isSuperAdmin,
        int? requestedKurumId,
        CancellationToken cancellationToken)
    {
        if (requestedKurumId.HasValue)
        {
            if (requestedKurumId.Value <= 0)
            {
                throw new BaseException("KurumId zorunludur.", 400);
            }

            if (!isSuperAdmin)
            {
                var hasAccess = kurumIds.Contains(requestedKurumId.Value) ||
                                await _identityStore.UserHasKurumAccessAsync(userId, requestedKurumId.Value, cancellationToken);
                if (!hasAccess)
                {
                    throw new BaseException("Bu kurum icin yetkiniz bulunmuyor.", 403);
                }
            }

            return requestedKurumId.Value;
        }

        if (defaultKurumId.HasValue)
        {
            return defaultKurumId.Value;
        }

        if (kurumIds.Count > 0)
        {
            return kurumIds.OrderBy(x => x).First();
        }

        if (isSuperAdmin)
        {
            return null;
        }

        throw new BaseException("Kullanıcının kurum erişimi bulunmuyor.", 401);
    }
}

public class AuthenticationService : AuthenticationService<Guid>, IAuthenticationService
{
    public AuthenticationService(
        IIdentityStore<Guid> identityStore,
        IJwtTokenService tokenService,
        ICurrentUserAccessor currentUserAccessor,
        ICurrentTenantAccessor currentTenantAccessor,
        IPasswordHasher passwordHasher,
        IOptions<JwtTokenOptions> jwtTokenOptions)
        : base(identityStore, tokenService, currentUserAccessor, currentTenantAccessor, passwordHasher, jwtTokenOptions)
    {
    }
}
