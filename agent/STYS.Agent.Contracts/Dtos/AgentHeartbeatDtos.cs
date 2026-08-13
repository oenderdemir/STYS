using STYS.Agent.Contracts.Enums;
using STYS.Agent.Contracts.Versioning;

namespace STYS.Agent.Contracts.Dtos;

public sealed class AgentHeartbeatRequest
{
    public string AgentVersion { get; set; } = string.Empty;
    public string ContractVersion { get; set; } = AgentContractVersion.Current;
    public string RuntimeIdentifier { get; set; } = string.Empty;
    public IReadOnlyCollection<string> SupportedApiVersions { get; set; } = [];
    public IReadOnlyCollection<string> SupportedCapabilities { get; set; } = [];
    public IReadOnlyCollection<AgentModuleInfo> InstalledModules { get; set; } = [];
    public string? CihazKimligi { get; set; }
    public string? Platform { get; set; }
    public string? OsVersion { get; set; }
}

public sealed class AgentModuleInfo
{
    public string ModuleName { get; set; } = string.Empty;
    public string ModuleVersion { get; set; } = string.Empty;
}

public sealed class AgentHeartbeatResponse
{
    public string? MinimumSupportedAgentVersion { get; set; }
    public string? RecommendedAgentVersion { get; set; }
    public string? SupportedContractVersion { get; set; }
    public string? LatestAgentVersion { get; set; }
    public string? RequiredContractVersion { get; set; }
    public AgentCompatibilityStatus CompatibilityStatus { get; set; }
    public IReadOnlyCollection<string> DeprecatedCapabilities { get; set; } = [];
    public bool RequiredUpdate { get; set; }
}
