using STYS.Agent.Contracts.Versioning;

namespace STYS.Agent.Options;

public sealed class AgentCompatibilityOptions
{
    public const string SectionName = "AgentCompatibility";

    public string MinimumSupportedAgentVersion { get; set; } = "1.0.0";
    public string RecommendedAgentVersion { get; set; } = "1.0.0";
    public string SupportedContractVersion { get; set; } = AgentContractVersion.Current;
}
