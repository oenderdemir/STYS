using STYS.Agent.Client.Commands;

namespace STYS.Agent.Upgrade;

public sealed class AgentApplyUpgradeCommand : IAgentCommand
{
    public string CommandType => "AgentApplyUpgrade";

    public Guid CommandId { get; set; }
    public int ReleaseId { get; set; }
    public string Version { get; set; } = string.Empty;
    public string RuntimeIdentifier { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
}
