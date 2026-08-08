namespace STYS.Agent.Authorization;

public interface ICurrentAgentContext
{
    int AgentId { get; }
    string AgentInstanceId { get; }
    int KurumId { get; }
    IReadOnlyCollection<int> TesisIds { get; }
    IReadOnlyCollection<string> Scopes { get; }
    int CredentialVersion { get; }
    bool IsAuthenticated { get; }
}
