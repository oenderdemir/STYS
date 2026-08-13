using STYS.Agent.Contracts.Enums;
using STYS.Agent.Options;

namespace STYS.Agent.Services;

public sealed record AgentCompatibilityEvaluation(
    AgentCompatibilityStatus CompatibilityStatus,
    string? AgentVersion,
    string? ContractVersion,
    string? MinimumSupportedAgentVersion,
    string? RecommendedAgentVersion,
    string? SupportedContractVersion)
{
    public bool RequiredUpdate =>
        CompatibilityStatus is AgentCompatibilityStatus.Unknown
            or AgentCompatibilityStatus.UpdateRequired
            or AgentCompatibilityStatus.IncompatibleContract;
}

public static class AgentCompatibilityEvaluator
{
    public static AgentCompatibilityEvaluation Evaluate(
        string? agentVersion,
        string? contractVersion,
        AgentCompatibilityOptions options)
    {
        var minimum = AgentSemVer.NormalizeVersionText(options.MinimumSupportedAgentVersion);
        var recommended = AgentSemVer.NormalizeVersionText(options.RecommendedAgentVersion);
        var supportedContract = AgentSemVer.NormalizeVersionText(options.SupportedContractVersion);

        if (!AgentSemVer.TryParse(minimum, out var minimumVersion)
            || !AgentSemVer.TryParse(recommended, out var recommendedVersion)
            || !AgentSemVer.TryParse(supportedContract, out var supportedContractVersion))
        {
            return BuildEvaluation(AgentCompatibilityStatus.Unknown, agentVersion, contractVersion, minimum, recommended, supportedContract);
        }

        if (!AgentSemVer.TryParse(agentVersion, out var currentAgentVersion)
            || !AgentSemVer.TryParse(contractVersion, out var currentContractVersion))
        {
            return BuildEvaluation(AgentCompatibilityStatus.Unknown, agentVersion, contractVersion, minimum, recommended, supportedContract);
        }

        if (currentContractVersion.CompareTo(supportedContractVersion) != 0)
        {
            return BuildEvaluation(AgentCompatibilityStatus.IncompatibleContract, agentVersion, contractVersion, minimum, recommended, supportedContract);
        }

        if (currentAgentVersion.CompareTo(minimumVersion) < 0)
        {
            return BuildEvaluation(AgentCompatibilityStatus.UpdateRequired, agentVersion, contractVersion, minimum, recommended, supportedContract);
        }

        var effectiveRecommended = recommendedVersion.CompareTo(minimumVersion) < 0
            ? minimumVersion
            : recommendedVersion;

        if (currentAgentVersion.CompareTo(effectiveRecommended) >= 0)
        {
            return BuildEvaluation(AgentCompatibilityStatus.Supported, agentVersion, contractVersion, minimum, recommended, supportedContract);
        }

        return BuildEvaluation(AgentCompatibilityStatus.UpdateAvailable, agentVersion, contractVersion, minimum, recommended, supportedContract);
    }

    public static bool CanStartPayment(AgentCompatibilityStatus status) =>
        status is AgentCompatibilityStatus.Supported or AgentCompatibilityStatus.UpdateAvailable;

    private static AgentCompatibilityEvaluation BuildEvaluation(
        AgentCompatibilityStatus status,
        string? agentVersion,
        string? contractVersion,
        string? minimumSupportedAgentVersion,
        string? recommendedAgentVersion,
        string? supportedContractVersion) =>
        new(
            status,
            AgentSemVer.NormalizeVersionText(agentVersion),
            AgentSemVer.NormalizeVersionText(contractVersion),
            minimumSupportedAgentVersion,
            recommendedAgentVersion,
            supportedContractVersion);
}
