namespace STYS.Agent.Modules.Pavo;

public sealed class PavoAgentOptions
{
    public const string SectionName = "Pavo";
    public const string FingerprintEnvironmentVariable = "STYS_PAVO_FINGERPRINT";

    private const string DefaultFingerprintValue = "STYS.Agent";
    private const int DefaultTimeoutSecondsValue = 180;
    private const int DefaultConnectTimeoutSecondsValue = 5;

    public string Fingerprint { get; set; } = DefaultFingerprintValue;
    public int TimeoutSeconds { get; set; } = DefaultTimeoutSecondsValue;

    /// <summary>TCP connect ceiling; see <see cref="ResolveConnectTimeoutSeconds"/>.</summary>
    public int ConnectTimeoutSeconds { get; set; } = DefaultConnectTimeoutSecondsValue;

    public static string ResolveFingerprint(string? configured, string? environmentOverride)
    {
        if (!string.IsNullOrWhiteSpace(environmentOverride))
        {
            return environmentOverride.Trim();
        }

        return string.IsNullOrWhiteSpace(configured) ? DefaultFingerprintValue : configured.Trim();
    }

    public static int ResolveTimeoutSeconds(int configured) => configured > 0 ? configured : DefaultTimeoutSecondsValue;

    /// <summary>
    /// Bounds only the TCP connect phase. Kept short because a device on the local network either
    /// answers the handshake promptly or is not reachable at all; the full request timeout still
    /// applies once the connection is up.
    /// </summary>
    public static int ResolveConnectTimeoutSeconds(int configured) =>
        configured > 0 ? configured : DefaultConnectTimeoutSecondsValue;
}
