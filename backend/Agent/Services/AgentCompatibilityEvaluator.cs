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
        var minimum = NormalizeVersionString(options.MinimumSupportedAgentVersion);
        var recommended = NormalizeVersionString(options.RecommendedAgentVersion);
        var supportedContract = NormalizeVersionString(options.SupportedContractVersion);

        if (!TryParseVersion(minimum, out var minimumVersion)
            || !TryParseVersion(recommended, out var recommendedVersion)
            || !TryParseVersion(supportedContract, out var supportedContractVersion))
        {
            return BuildEvaluation(AgentCompatibilityStatus.Unknown, agentVersion, contractVersion, minimum, recommended, supportedContract);
        }

        if (!TryParseVersion(agentVersion, out var currentAgentVersion)
            || !TryParseVersion(contractVersion, out var currentContractVersion))
        {
            return BuildEvaluation(AgentCompatibilityStatus.Unknown, agentVersion, contractVersion, minimum, recommended, supportedContract);
        }

        if (!currentContractVersion.Equals(supportedContractVersion))
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
        new(status, NormalizeVersionString(agentVersion), NormalizeVersionString(contractVersion), minimumSupportedAgentVersion, recommendedAgentVersion, supportedContractVersion);

    private static bool TryParseVersion(string? value, out NormalizedVersion version)
    {
        version = default;

        var normalized = NormalizeVersionString(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        var parts = normalized.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length is < 1 or > 4)
        {
            return false;
        }

        if (!TryReadPart(parts, 0, out var major)
            || !TryReadPart(parts, 1, out var minor)
            || !TryReadPart(parts, 2, out var patch)
            || !TryReadPart(parts, 3, out var revision))
        {
            return false;
        }

        version = new NormalizedVersion(major, minor, patch, revision);
        return true;
    }

    private static bool TryReadPart(IReadOnlyList<string> parts, int index, out int value)
    {
        value = 0;
        if (index >= parts.Count)
        {
            return true;
        }

        return int.TryParse(parts[index], out value);
    }

    private static string? NormalizeVersionString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[1..];
        }

        var separatorIndex = trimmed.IndexOfAny(['-', '+']);
        if (separatorIndex >= 0)
        {
            trimmed = trimmed[..separatorIndex];
        }

        return trimmed;
    }

    private readonly record struct NormalizedVersion(int Major, int Minor, int Patch, int Revision) : IComparable<NormalizedVersion>
    {
        public int CompareTo(NormalizedVersion other)
        {
            var major = Major.CompareTo(other.Major);
            if (major != 0) return major;

            var minor = Minor.CompareTo(other.Minor);
            if (minor != 0) return minor;

            var patch = Patch.CompareTo(other.Patch);
            if (patch != 0) return patch;

            return Revision.CompareTo(other.Revision);
        }
    }
}
