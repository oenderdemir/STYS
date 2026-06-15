using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TOD.Platform.Security.Auth.DTO;
using TOD.Platform.Security.Auth.Options;

namespace TOD.Platform.Security.Auth.Services;

public class JwtTokenService : IJwtTokenService
{
    private readonly IOptions<JwtTokenOptions> _jwtOptions;

    public JwtTokenService(IOptions<JwtTokenOptions> jwtOptions)
    {
        _jwtOptions = jwtOptions;
    }

    public Task<GenerateTokenResponse> GenerateToken(GenerateTokenRequest request, CancellationToken cancellationToken = default)
    {
        var options = _jwtOptions.Value;
        if (string.IsNullOrWhiteSpace(options.Key))
        {
            throw new InvalidOperationException("JWT Key is not configured properly.");
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Key));
        var now = DateTime.UtcNow;
        var expiresAt = now.AddMinutes(options.AccessTokenExpirationMinutes);
        var jti = Guid.NewGuid().ToString("N");

        var claims = new List<Claim>
        {
            new("userName", request.UserName),
            new(ClaimTypes.Name, request.Name),
            new(ClaimTypes.Email, request.Email),
            new(ClaimTypes.Surname, request.Surname),
            new(ClaimTypes.NameIdentifier, request.UserName),
            new(JwtRegisteredClaimNames.Jti, jti),
            new("tokenVersion", request.TokenVersion.ToString())
        };

        if (!string.IsNullOrWhiteSpace(request.UserId))
        {
            claims.Add(new Claim("userId", request.UserId));
        }

        if (request.KurumId.HasValue)
        {
            claims.Add(new Claim("kurumId", request.KurumId.Value.ToString()));
        }

        if (request.KurumIds.Count > 0)
        {
            claims.Add(new Claim("kurumIds", string.Join(",", request.KurumIds.Distinct())));
        }

        claims.Add(new Claim("isKurumAdmin", request.IsKurumAdmin.ToString().ToLowerInvariant()));
        claims.Add(new Claim("isSuperAdmin", request.IsSuperAdmin.ToString().ToLowerInvariant()));

        var jwt = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            notBefore: now,
            expires: expiresAt,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        var token = new JwtSecurityTokenHandler().WriteToken(jwt);

        return Task.FromResult(new GenerateTokenResponse
        {
            Token = token,
            TokenExpireDate = expiresAt,
            Jti = jti
        });
    }
}
