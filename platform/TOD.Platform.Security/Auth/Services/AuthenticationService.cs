using TOD.Platform.Security.Auth.DTO;
using TOD.Platform.Security.Auth.Models;

namespace TOD.Platform.Security.Auth.Services;

public class AuthenticationService<TKey> : IAuthenticationService<TKey> where TKey : struct
{
    private readonly IIdentityStore<TKey> _identityStore;
    private readonly IJwtTokenService _tokenService;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IPasswordHasher _passwordHasher;

    public AuthenticationService(
        IIdentityStore<TKey> identityStore,
        IJwtTokenService tokenService,
        ICurrentUserAccessor currentUserAccessor,
        IPasswordHasher passwordHasher)
    {
        _identityStore = identityStore;
        _tokenService = tokenService;
        _currentUserAccessor = currentUserAccessor;
        _passwordHasher = passwordHasher;
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

        return await LogoutAsync();
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

        var permissions = await _identityStore.GetPermissionsAsync(user.Id, cancellationToken);
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

        var generatedToken = await _tokenService.GenerateToken(new GenerateTokenRequest
        {
            UserName = user.UserName,
            Name = user.Name ?? string.Empty,
            Surname = user.Surname ?? string.Empty,
            Email = user.Email ?? string.Empty,
            Permissions = normalizedPermissions
        }, cancellationToken);

        return new LoginResponseDto
        {
            AuthenticateResult = true,
            AuthToken = generatedToken.Token,
            AccessTokenExpireDate = generatedToken.TokenExpireDate,
            UserStatus = user.Status
        };
    }

    public Task<LoginResponseDto> LogoutAsync()
    {
        return Task.FromResult(new LoginResponseDto
        {
            AuthenticateResult = false,
            AuthToken = string.Empty,
            AccessTokenExpireDate = DateTime.UtcNow
        });
    }

    private static LoginResponseDto CreateUnauthorizedResponse()
    {
        return new LoginResponseDto
        {
            AuthenticateResult = false,
            AuthToken = string.Empty,
            AccessTokenExpireDate = DateTime.UtcNow
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
}

public class AuthenticationService : AuthenticationService<Guid>, IAuthenticationService
{
    public AuthenticationService(
        IIdentityStore<Guid> identityStore,
        IJwtTokenService tokenService,
        ICurrentUserAccessor currentUserAccessor,
        IPasswordHasher passwordHasher)
        : base(identityStore, tokenService, currentUserAccessor, passwordHasher)
    {
    }
}
