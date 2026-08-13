using System.Security.Cryptography;

namespace STYS.Agent.Client.Upgrade;

public static class AgentReleaseTrustMaterial
{
    public const string PublicKeyPemEnvironmentVariable = "STYS_AGENT_RELEASE_PUBLIC_KEY_PEM";
    public const string PublicKeyPemPathEnvironmentVariable = "STYS_AGENT_RELEASE_PUBLIC_KEY_PEM_PATH";

    public static string Resolve(string? configuredPem, string? configuredPath)
    {
        var pem = ResolveConfiguredValue(configuredPem, configuredPath);
        if (!string.IsNullOrWhiteSpace(pem))
        {
            return pem.Trim();
        }

        pem = ResolveEnvironmentValue(PublicKeyPemEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(pem))
        {
            return pem.Trim();
        }

        var path = ResolveEnvironmentValue(PublicKeyPemPathEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(path))
        {
            return ReadPemFromPath(path);
        }

        return ReadPemFromPath(GetDefaultPublicKeyPath());
    }

    private static string ResolveConfiguredValue(string? configuredPem, string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPem))
        {
            return configuredPem;
        }

        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return ReadPemFromPath(configuredPath);
        }

        return string.Empty;
    }

    private static string ResolveEnvironmentValue(string variable)
    {
        var value = Environment.GetEnvironmentVariable(variable);
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value;
    }

    private static string ReadPemFromPath(string path)
    {
        var fullPath = Path.GetFullPath(path.Trim());
        if (!File.Exists(fullPath))
        {
            throw new InvalidOperationException($"Release public key bulunamadı: {fullPath}");
        }

        return File.ReadAllText(fullPath);
    }

    private static string GetDefaultPublicKeyPath()
    {
        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "STYS", "AgentTrust", "release-public-key.pem");
        }

        if (OperatingSystem.IsLinux())
        {
            return "/etc/stys-agent/trust/release-public-key.pem";
        }

        return Path.Combine(AppContext.BaseDirectory, "trust", "release-public-key.pem");
    }
}
