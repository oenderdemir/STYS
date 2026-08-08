namespace STYS.Agent.Contracts.Dtos;

public sealed class AgentTokenRequest
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string AgentInstanceId { get; set; } = string.Empty;
    public string? AgentVersion { get; set; }
}

public sealed class AgentTokenResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public string TokenType { get; set; } = "Bearer";
}

public sealed class AgentTokenRefreshRequest
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
}
