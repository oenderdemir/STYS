using STYS.Agent.Contracts.Dtos;

namespace STYS.Agent.Client.Authentication;

public sealed class AgentTokenStore
{
    private string? _accessToken;
    private DateTime _expiresAt;
    private readonly object _lock = new();

    public void SetToken(AgentTokenResponse token)
    {
        lock (_lock)
        {
            _accessToken = token.AccessToken;
            _expiresAt = token.ExpiresAt;
        }
    }

    public bool HasValidToken()
    {
        lock (_lock)
        {
            return !string.IsNullOrWhiteSpace(_accessToken) && DateTime.UtcNow < _expiresAt.Subtract(TimeSpan.FromMinutes(1));
        }
    }

    public string? GetToken()
    {
        lock (_lock)
        {
            return _accessToken;
        }
    }

    public void ClearToken()
    {
        lock (_lock)
        {
            _accessToken = null;
            _expiresAt = DateTime.MinValue;
        }
    }
}
