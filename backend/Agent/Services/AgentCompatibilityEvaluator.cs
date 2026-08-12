using System.Globalization;
using System.Numerics;
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
        var minimum = NormalizeVersionText(options.MinimumSupportedAgentVersion);
        var recommended = NormalizeVersionText(options.RecommendedAgentVersion);
        var supportedContract = NormalizeVersionText(options.SupportedContractVersion);

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
            NormalizeVersionText(agentVersion),
            NormalizeVersionText(contractVersion),
            minimumSupportedAgentVersion,
            recommendedAgentVersion,
            supportedContractVersion);

    private static bool TryParseVersion(string? value, out SemanticVersion version)
    {
        version = default;

        var normalized = NormalizeVersionText(value);
        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        var buildIndex = normalized.IndexOf('+');
        if (buildIndex >= 0)
        {
            if (buildIndex == normalized.Length - 1)
                return false;

            normalized = normalized[..buildIndex];
        }

        var prereleaseIndex = normalized.IndexOf('-');
        string? prerelease = null;
        if (prereleaseIndex >= 0)
        {
            if (prereleaseIndex == normalized.Length - 1)
                return false;

            prerelease = normalized[(prereleaseIndex + 1)..];
            normalized = normalized[..prereleaseIndex];
        }

        var coreParts = normalized.Split('.', StringSplitOptions.None);
        if (coreParts.Length != 3)
            return false;

        if (!TryParseCorePart(coreParts[0], out var major)
            || !TryParseCorePart(coreParts[1], out var minor)
            || !TryParseCorePart(coreParts[2], out var patch))
        {
            return false;
        }

        if (!TryParsePrerelease(prerelease, out var prereleaseIdentifiers))
            return false;

        version = new SemanticVersion(major, minor, patch, prereleaseIdentifiers);
        return true;
    }

    private static bool TryParseCorePart(string part, out int value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(part))
            return false;

        if (part.Length > 1 && part[0] == '0')
            return false;

        return int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryParsePrerelease(string? prerelease, out IReadOnlyList<SemanticIdentifier> identifiers)
    {
        identifiers = Array.Empty<SemanticIdentifier>();

        if (prerelease is null)
            return true;

        var parts = prerelease.Split('.', StringSplitOptions.None);
        if (parts.Length == 0)
            return false;

        var values = new SemanticIdentifier[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            var identifier = parts[i];
            if (string.IsNullOrWhiteSpace(identifier) || !IsValidIdentifier(identifier))
                return false;

            if (IsNumericIdentifier(identifier))
            {
                if (identifier.Length > 1 && identifier[0] == '0')
                    return false;

                values[i] = SemanticIdentifier.Numeric(BigInteger.Parse(identifier, CultureInfo.InvariantCulture));
                continue;
            }

            values[i] = SemanticIdentifier.FromText(identifier);
        }

        identifiers = values;
        return true;
    }

    private static bool IsValidIdentifier(string value) =>
        value.All(character => char.IsLetterOrDigit(character) || character == '-');

    private static bool IsNumericIdentifier(string value) =>
        value.All(char.IsDigit);

    private static string? NormalizeVersionText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        if (trimmed.Length > 1 && trimmed[0] is 'v' or 'V' && char.IsDigit(trimmed[1]))
            trimmed = trimmed[1..];

        return trimmed;
    }

    private readonly record struct SemanticVersion(int Major, int Minor, int Patch, IReadOnlyList<SemanticIdentifier> Prerelease) : IComparable<SemanticVersion>
    {
        public int CompareTo(SemanticVersion other)
        {
            var major = Major.CompareTo(other.Major);
            if (major != 0) return major;

            var minor = Minor.CompareTo(other.Minor);
            if (minor != 0) return minor;

            var patch = Patch.CompareTo(other.Patch);
            if (patch != 0) return patch;

            var prereleaseComparison = ComparePrerelease(Prerelease, other.Prerelease);
            return prereleaseComparison;
        }
    }

    private readonly record struct SemanticIdentifier(bool IsNumeric, BigInteger NumericValue, string Text) : IComparable<SemanticIdentifier>
    {
        public static SemanticIdentifier Numeric(BigInteger value) => new(true, value, value.ToString(CultureInfo.InvariantCulture));
        public static SemanticIdentifier FromText(string value) => new(false, default, value);

        public int CompareTo(SemanticIdentifier other)
        {
            if (IsNumeric && other.IsNumeric)
                return NumericValue.CompareTo(other.NumericValue);

            if (IsNumeric)
                return -1;

            if (other.IsNumeric)
                return 1;

            return string.Compare(Text, other.Text, StringComparison.Ordinal);
        }
    }

    private static int ComparePrerelease(IReadOnlyList<SemanticIdentifier> left, IReadOnlyList<SemanticIdentifier> right)
    {
        var leftEmpty = left.Count == 0;
        var rightEmpty = right.Count == 0;

        if (leftEmpty && rightEmpty)
            return 0;

        if (leftEmpty)
            return 1;

        if (rightEmpty)
            return -1;

        var count = Math.Min(left.Count, right.Count);
        for (var i = 0; i < count; i++)
        {
            var comparison = left[i].CompareTo(right[i]);
            if (comparison != 0)
                return comparison;
        }

        return left.Count.CompareTo(right.Count);
    }
}
