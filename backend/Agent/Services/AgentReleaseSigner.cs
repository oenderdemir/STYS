using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using STYS.Agent.Options;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Agent.Services;

public interface IAgentReleaseSigner
{
    /// <summary>Signs a canonical release manifest payload and returns the Base64 signature.</summary>
    string SignManifest(byte[] payload);

    /// <summary>
    /// Public key (PEM) matching the configured signing key, for operators verifying that the
    /// trust anchor they provisioned to agents belongs to the same chain. Never exposes the
    /// private key.
    /// </summary>
    string ExportPublicKeyPem();
}

/// <summary>
/// Signs release manifests with RSA-PSS/SHA-256. The algorithm must stay byte-compatible with
/// AgentReleaseSignatureVerifier on the agent side, which calls
/// <c>rsa.VerifyData(payload, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss)</c>.
/// </summary>
public sealed class AgentReleaseSigner : IAgentReleaseSigner
{
    public const string PrivateKeyNotConfiguredMessage = "Agent release signing private key yapılandırılmamış.";

    private readonly AgentReleasePublishingOptions _options;

    public AgentReleaseSigner(IOptions<AgentReleasePublishingOptions>? options = null)
    {
        _options = options?.Value ?? new AgentReleasePublishingOptions();
    }

    public string SignManifest(byte[] payload)
    {
        using var rsa = LoadPrivateKey();
        var signature = rsa.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        return Convert.ToBase64String(signature);
    }

    public string ExportPublicKeyPem()
    {
        using var rsa = LoadPrivateKey();
        return rsa.ExportSubjectPublicKeyInfoPem();
    }

    private RSA LoadPrivateKey()
    {
        var path = _options.SigningPrivateKeyPemPath?.Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new BaseException(PrivateKeyNotConfiguredMessage, 500);
        }

        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            // The path is operator-supplied configuration, not user input, so naming it helps
            // diagnosis. The key CONTENTS are never surfaced.
            throw new BaseException($"Agent release signing private key bulunamadı: {fullPath}", 500);
        }

        var rsa = RSA.Create();
        try
        {
            rsa.ImportFromPem(File.ReadAllText(fullPath));
        }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException)
        {
            rsa.Dispose();
            throw new BaseException("Agent release signing private key geçerli bir RSA PEM değil.", 500);
        }
        catch
        {
            rsa.Dispose();
            throw;
        }

        return rsa;
    }
}
