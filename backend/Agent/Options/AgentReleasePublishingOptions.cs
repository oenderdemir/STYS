namespace STYS.Agent.Options;

/// <summary>
/// Configuration for publishing signed agent releases. Both values are optional so that
/// deployments which never publish releases start normally; publishing itself fails with an
/// explicit message when they are missing (fail-on-use rather than fail-fast at startup).
/// </summary>
public sealed class AgentReleasePublishingOptions
{
    public const string SectionName = "AgentReleasePublishing";

    /// <summary>
    /// Root directory that holds uploaded release packages. Kept outside the web root so packages
    /// are never statically servable; downloads go through the authenticated agent endpoint.
    /// </summary>
    public string? StorageRootPath { get; set; }

    /// <summary>
    /// Path to the RSA private key (PEM) used to sign release manifests. The matching public key is
    /// what installers provision as the agent trust anchor. Never logged, never returned to
    /// clients, never stored in the database or inside a package.
    /// </summary>
    public string? SigningPrivateKeyPemPath { get; set; }

    /// <summary>Upload size ceiling. Guards against a single request exhausting disk.</summary>
    public long MaxPackageSizeBytes { get; set; } = 512L * 1024 * 1024;
}
