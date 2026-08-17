namespace STYS.Agent.Contracts.Versioning;

/// <summary>
/// Compares agent versions for release identity.
///
/// The .NET SDK appends the source revision to AssemblyInformationalVersion when building inside a
/// git repository, so an agent built as 1.0.1 reports "1.0.1+&lt;commit-sha&gt;" at runtime. Build
/// metadata carries no release identity under SemVer, so it is ignored here — otherwise a perfectly
/// healthy upgrade looks like the wrong build and gets rolled back.
///
/// Only build metadata is discarded. Major/minor/patch and prerelease are compared verbatim, so
/// 1.0.1 and 1.0.2 differ, and 1.0.1-beta.1, 1.0.1-beta.2 and 1.0.1 are all distinct.
///
/// This lives in Contracts rather than reusing the backend's AgentSemVer because the updater only
/// references Contracts.
/// </summary>
public static class AgentVersionComparison
{
    /// <summary>
    /// Removes the SemVer build metadata suffix. Everything from the first '+' onwards is metadata;
    /// a prerelease label sits before it and is preserved.
    /// </summary>
    public static string StripBuildMetadata(string? version)
    {
        var trimmed = version?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return string.Empty;
        }

        var plusIndex = trimmed.IndexOf('+');
        return plusIndex < 0 ? trimmed : trimmed[..plusIndex].Trim();
    }

    /// <summary>
    /// True when both values name the same release once build metadata is set aside. Empty or
    /// missing values never match, so a version the agent failed to report is not treated as a
    /// successful upgrade.
    /// </summary>
    public static bool SameRelease(string? left, string? right)
    {
        var normalizedLeft = StripBuildMetadata(left);
        var normalizedRight = StripBuildMetadata(right);

        if (normalizedLeft.Length == 0 || normalizedRight.Length == 0)
        {
            return false;
        }

        return string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
    }
}
