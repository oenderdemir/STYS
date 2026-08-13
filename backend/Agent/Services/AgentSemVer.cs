using System.Globalization;
using System.Numerics;

namespace STYS.Agent.Services;

public static class AgentSemVer
{
    public static bool TryParse(string? value, out AgentSemanticVersion version)
    {
        version = default;

        var normalized = NormalizeVersionText(value);
        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        var buildIndex = normalized.IndexOf('+');
        string? buildMetadata = null;
        if (buildIndex >= 0)
        {
            if (normalized.IndexOf('+', buildIndex + 1) >= 0)
                return false;

            if (buildIndex == normalized.Length - 1)
                return false;

            buildMetadata = normalized[(buildIndex + 1)..];
            normalized = normalized[..buildIndex];
        }

        var prereleaseIndex = normalized.IndexOf('-');
        string? prerelease = null;
        if (prereleaseIndex >= 0)
        {
            if (normalized.IndexOf('-', prereleaseIndex + 1) >= 0)
            {
                // prerelease kısmında ek '-' karakterleri serbesttir; yine de core bölümün
                // ilk '-' ayırıcısı kullanılır. Bu blok, core kısmın boş kalmasını önler.
            }

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

        if (!TryValidateBuildMetadata(buildMetadata))
            return false;

        version = new AgentSemanticVersion(major, minor, patch, prereleaseIdentifiers);
        return true;
    }

    public static string? NormalizeVersionText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        if (trimmed.Length > 1 && trimmed[0] is 'v' or 'V' && char.IsDigit(trimmed[1]))
            trimmed = trimmed[1..];

        return trimmed;
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

    private static bool TryValidateBuildMetadata(string? buildMetadata)
    {
        if (buildMetadata is null)
            return true;

        var parts = buildMetadata.Split('.', StringSplitOptions.None);
        if (parts.Length == 0)
            return false;

        foreach (var part in parts)
        {
            if (string.IsNullOrWhiteSpace(part) || !IsValidIdentifier(part))
                return false;
        }

        return true;
    }

    private static bool IsValidIdentifier(string value) =>
        value.All(character =>
            (character is >= '0' and <= '9')
            || (character is >= 'A' and <= 'Z')
            || (character is >= 'a' and <= 'z')
            || character == '-');

    private static bool IsNumericIdentifier(string value) =>
        value.All(char.IsDigit);
}

public readonly record struct AgentSemanticVersion(
    int Major,
    int Minor,
    int Patch,
    IReadOnlyList<SemanticIdentifier> Prerelease) : IComparable<AgentSemanticVersion>
{
    public int CompareTo(AgentSemanticVersion other)
    {
        var major = Major.CompareTo(other.Major);
        if (major != 0) return major;

        var minor = Minor.CompareTo(other.Minor);
        if (minor != 0) return minor;

        var patch = Patch.CompareTo(other.Patch);
        if (patch != 0) return patch;

        return ComparePrerelease(Prerelease, other.Prerelease);
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

public readonly record struct SemanticIdentifier(bool IsNumeric, BigInteger NumericValue, string Text) : IComparable<SemanticIdentifier>
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
