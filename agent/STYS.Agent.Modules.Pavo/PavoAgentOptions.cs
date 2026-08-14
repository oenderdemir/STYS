namespace STYS.Agent.Modules.Pavo;

public sealed class PavoAgentOptions
{
    public const string SectionName = "Pavo";
    public const string FingerprintEnvironmentVariable = "STYS_PAVO_FINGERPRINT";

    private const string DefaultFingerprintValue = "STYS.Agent";
    private const int DefaultTimeoutSecondsValue = 180;

    public string Fingerprint { get; set; } = DefaultFingerprintValue;
    public int TimeoutSeconds { get; set; } = DefaultTimeoutSecondsValue;

    public static string ResolveFingerprint(string? configured, string? environmentOverride)
    {
        if (!string.IsNullOrWhiteSpace(environmentOverride))
        {
            return environmentOverride.Trim();
        }

        return string.IsNullOrWhiteSpace(configured) ? DefaultFingerprintValue : configured.Trim();
    }

    public static int ResolveTimeoutSeconds(int configured) => configured > 0 ? configured : DefaultTimeoutSecondsValue;
}
