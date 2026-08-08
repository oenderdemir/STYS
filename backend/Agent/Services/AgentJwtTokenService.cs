using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using STYS.Agent.Contracts.Dtos;
using TOD.Platform.Security.Auth.Options;

namespace STYS.Agent.Services;

public sealed class AgentJwtTokenService : IAgentJwtTokenService
{
    private readonly IOptions<JwtTokenOptions> _jwtOptions;

    public AgentJwtTokenService(IOptions<JwtTokenOptions> jwtOptions)
    {
        _jwtOptions = jwtOptions;
    }

    public Task<AgentTokenResponse> GenerateTokenAsync(AgentTokenDescriptor descriptor, CancellationToken cancellationToken)
    {
        var options = _jwtOptions.Value;
        if (string.IsNullOrWhiteSpace(options.Key))
            throw new InvalidOperationException("JWT Key is not configured properly.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Key));
        var now = DateTime.UtcNow;
        var expiresAt = now.AddMinutes(descriptor.AccessTokenExpirationMinutes);
        var jti = Guid.NewGuid().ToString("N");

        var claims = new List<Claim>
        {
            new("agentId", descriptor.AgentId.ToString()),
            new("agentKey", descriptor.AgentKey),
            new("agentInstanceId", descriptor.AgentInstanceId),
            new("kurumId", descriptor.KurumId.ToString()),
            new("agentTesisIds", string.Join(",", descriptor.TesisIds)),
            new("agentScopes", string.Join(",", descriptor.Scopes)),
            new("credentialId", descriptor.CredentialId.ToString()),
            new("credentialVersion", descriptor.CredentialVersion.ToString()),
            new("tokenType", "agent"),
            new(JwtRegisteredClaimNames.Jti, jti),
            new(JwtRegisteredClaimNames.Sub, $"agent:{descriptor.AgentId}"),
            new(JwtRegisteredClaimNames.Iat, new DateTimeOffset(now).ToUnixTimeSeconds().ToString())
        };

        if (!string.IsNullOrWhiteSpace(descriptor.AgentVersion))
            claims.Add(new Claim("agentVersion", descriptor.AgentVersion));

        var jwt = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            notBefore: now,
            expires: expiresAt,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        var token = new JwtSecurityTokenHandler().WriteToken(jwt);

        return Task.FromResult(new AgentTokenResponse
        {
            AccessToken = token,
            ExpiresAt = expiresAt,
            TokenType = "Bearer"
        });
    }
}
