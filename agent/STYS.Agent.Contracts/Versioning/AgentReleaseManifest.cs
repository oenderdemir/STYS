using System.Globalization;
using System.Text;
using STYS.Agent.Contracts.Dtos;

namespace STYS.Agent.Contracts.Versioning;

public static class AgentReleaseManifest
{
    public static byte[] BuildSignaturePayload(
        string version,
        string contractVersion,
        string runtimeIdentifier,
        string sha256,
        long packageSize,
        DateTimeOffset publishedAt) =>
        Encoding.UTF8.GetBytes(BuildCanonicalString(version, contractVersion, runtimeIdentifier, sha256, packageSize, publishedAt));

    public static byte[] BuildSignaturePayload(AgentStageUpgradeRequest request) =>
        BuildSignaturePayload(
            request.Version,
            request.ContractVersion,
            request.RuntimeIdentifier,
            request.Sha256,
            request.PackageSize,
            request.PublishedAt);

    public static string BuildCanonicalString(
        string version,
        string contractVersion,
        string runtimeIdentifier,
        string sha256,
        long packageSize,
        DateTimeOffset publishedAt) =>
        string.Join("|",
            Normalize(version),
            Normalize(contractVersion),
            Normalize(runtimeIdentifier),
            Normalize(sha256),
            packageSize.ToString(CultureInfo.InvariantCulture),
            publishedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));

    private static string Normalize(string? value) => value?.Trim() ?? string.Empty;
}
