namespace STYS.Agent.Services;

public sealed class AgentTokenDescriptor
{
    public int AgentId { get; init; }
    public string AgentKey { get; init; } = string.Empty;
    public string? AgentVersion { get; init; }
    public int KurumId { get; init; }
    public IReadOnlyCollection<int> TesisIds { get; init; } = [];
    public IReadOnlyCollection<string> Scopes { get; init; } = [];
    public string AgentInstanceId { get; init; } = string.Empty;
    public int CredentialId { get; init; }
    public int CredentialVersion { get; init; }
    public int AccessTokenExpirationMinutes { get; init; } = 60;
}
