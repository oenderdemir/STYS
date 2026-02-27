using TOD.Platform.Security.Auth.DTO;

namespace TOD.Platform.Security.Auth.Services;

public interface IJwtTokenService
{
    Task<GenerateTokenResponse> GenerateToken(GenerateTokenRequest request, CancellationToken cancellationToken = default);
}
