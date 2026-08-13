using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using STYS.Agent.Contracts.Dtos;
using STYS.Agent.Contracts.Versioning;
using STYS.Agent.Options;

namespace STYS.Agent.Upgrade;

public static class AgentReleaseSignatureVerifier
{
    public static bool Verify(AgentStageUpgradeRequest request, string publicKeyPem)
    {
        if (string.IsNullOrWhiteSpace(publicKeyPem))
        {
            return false;
        }

        try
        {
            var payload = AgentReleaseManifest.BuildSignaturePayload(request);
            using var rsa = RSA.Create();
            rsa.ImportFromPem(publicKeyPem);
            var signature = Convert.FromBase64String(request.Signature);
            return rsa.VerifyData(payload, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        }
        catch
        {
            return false;
        }
    }
}

