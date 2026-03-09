using TOD.Platform.Security.Auth.DTO;

namespace TOD.Platform.Security.Auth.Services;

public interface IAuthenticationService<TKey> where TKey : struct
{
    Task<LoginResponseDto> ChangePassword(ChangePasswordRequestDto model, CancellationToken cancellationToken = default);

    Task<LoginResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);

    Task<LoginResponseDto> RefreshAsync(RefreshTokenRequestDto request, CancellationToken cancellationToken = default);

    Task<LoginResponseDto> LogoutAsync(CancellationToken cancellationToken = default);
}

public interface IAuthenticationService : IAuthenticationService<Guid>
{
}
