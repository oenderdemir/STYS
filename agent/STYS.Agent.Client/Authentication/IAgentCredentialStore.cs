namespace STYS.Agent.Client.Authentication;

public sealed class AgentLocalCredential
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string AgentInstanceId { get; set; } = string.Empty;
    public string? AgentKey { get; set; }
    public int AgentId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public interface IAgentCredentialStore
{
    Task<AgentLocalCredential?> GetAsync(CancellationToken cancellationToken);
    Task SaveAsync(AgentLocalCredential credential, CancellationToken cancellationToken);
    Task DeleteAsync(CancellationToken cancellationToken);
}
