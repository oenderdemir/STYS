namespace STYS.Agent.Client.Authentication;

public sealed class AgentLocalCredential
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string AgentInstanceId { get; set; } = string.Empty;
    public string? AgentKey { get; set; }
    public string? EnrollmentBaseUrl { get; set; }
    public int AgentId { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>Proof of possession minted once per installation BEFORE the first enrollment
    /// attempt and replayed on retries, so a registration whose response was lost can be recovered
    /// by this machine and no other. Stored here because this file is already DPAPI-protected; a
    /// record holding only this value is not a usable credential.</summary>
    public string? RegistrationNonce { get; set; }
}

public interface IAgentCredentialStore
{
    Task<AgentLocalCredential?> GetAsync(CancellationToken cancellationToken);
    Task SaveAsync(AgentLocalCredential credential, CancellationToken cancellationToken);
    Task DeleteAsync(CancellationToken cancellationToken);
}
