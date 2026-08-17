using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using STYS.Agent.Contracts.Dtos;
using STYS.Agent.Contracts.Versioning;
using STYS.Agent.Options;
using STYS.Agent.Services;
using STYS.Agent.Upgrade;
using Xunit;

namespace STYS.Tests.Agent;

/// <summary>
/// Covers the signing half of remote upgrade end to end: the backend signer produces a signature
/// that the real agent-side verifier accepts, and rejects it once anything the manifest covers
/// changes. Keys are generated per test — no key material is ever committed.
/// </summary>
public sealed class AgentReleaseSigningTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "stys-release-signing", Guid.NewGuid().ToString("N"));

    public AgentReleaseSigningTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
            // Temp cleanup must not fail a test run.
        }
    }

    private (AgentReleaseSigner Signer, string PublicKeyPem) NewSigner()
    {
        using var rsa = RSA.Create(3072);
        var privateKeyPath = Path.Combine(_tempDir, $"{Guid.NewGuid():N}.pem");
        File.WriteAllText(privateKeyPath, rsa.ExportPkcs8PrivateKeyPem());

        var signer = new AgentReleaseSigner(Options.Create(new AgentReleasePublishingOptions
        {
            SigningPrivateKeyPemPath = privateKeyPath
        }));

        return (signer, rsa.ExportSubjectPublicKeyInfoPem());
    }

    private static AgentStageUpgradeRequest NewStageRequest(byte[] packageBytes, int releaseId = 42) => new()
    {
        ReleaseId = releaseId,
        Version = "1.0.1",
        ContractVersion = "1.0.0",
        RuntimeIdentifier = "win-x64",
        Sha256 = Convert.ToHexString(SHA256.HashData(packageBytes)),
        PackageSize = packageBytes.LongLength,
        PublishedAt = new DateTimeOffset(2026, 8, 17, 9, 0, 0, TimeSpan.Zero),
        ReleaseNotes = "test"
    };

    private static string Sign(AgentReleaseSigner signer, AgentStageUpgradeRequest request) =>
        signer.SignManifest(AgentReleaseManifest.BuildSignaturePayload(
            request.ReleaseId,
            request.Version,
            request.ContractVersion,
            request.RuntimeIdentifier,
            request.Sha256,
            request.PackageSize,
            request.PublishedAt));

    // ---------------------------------------------------------------- A. happy path

    [Fact]
    public void ImzalananManifest_AgentTarafindakiDogrulayiciylaGecerli()
    {
        var (signer, publicKeyPem) = NewSigner();
        var package = RandomNumberGenerator.GetBytes(2048);
        var request = NewStageRequest(package);

        request.Signature = Sign(signer, request);

        Assert.True(AgentReleaseSignatureVerifier.Verify(request, publicKeyPem));
    }

    [Fact]
    public void ImzaBase64_VeSha256HexFormatindaUretilir()
    {
        var (signer, _) = NewSigner();
        var package = RandomNumberGenerator.GetBytes(1024);
        var request = NewStageRequest(package);

        request.Signature = Sign(signer, request);

        // Format compatibility with what the entity stores and the agent parses.
        Assert.Equal(Convert.ToHexString(SHA256.HashData(package)), request.Sha256);
        Assert.Equal(package.LongLength, request.PackageSize);
        var decoded = Convert.FromBase64String(request.Signature);
        Assert.NotEmpty(decoded);
    }

    // ---------------------------------------------------------------- B. tamper

    [Fact]
    public void PaketDegisince_DogrulamaBasarisiz()
    {
        var (signer, publicKeyPem) = NewSigner();
        var package = RandomNumberGenerator.GetBytes(2048);
        var request = NewStageRequest(package);
        request.Signature = Sign(signer, request);

        // Flip one byte: the hash in the manifest no longer describes the package.
        var tampered = (byte[])package.Clone();
        tampered[7] ^= 0xFF;
        request.Sha256 = Convert.ToHexString(SHA256.HashData(tampered));

        Assert.False(AgentReleaseSignatureVerifier.Verify(request, publicKeyPem));
    }

    [Theory]
    [InlineData("version")]
    [InlineData("releaseId")]
    [InlineData("packageSize")]
    [InlineData("publishedAt")]
    public void ManifestAlaniDegisince_DogrulamaBasarisiz(string field)
    {
        var (signer, publicKeyPem) = NewSigner();
        var request = NewStageRequest(RandomNumberGenerator.GetBytes(512));
        request.Signature = Sign(signer, request);

        switch (field)
        {
            case "version": request.Version = "9.9.9"; break;
            case "releaseId": request.ReleaseId += 1; break;
            case "packageSize": request.PackageSize += 1; break;
            case "publishedAt": request.PublishedAt = request.PublishedAt.AddSeconds(1); break;
        }

        Assert.False(AgentReleaseSignatureVerifier.Verify(request, publicKeyPem));
    }

    // ---------------------------------------------------------------- C. wrong key

    [Fact]
    public void BaskaAnahtarlaImzalanan_AgentTarafindanReddedilir()
    {
        var (signer, _) = NewSigner();
        var (_, otherPublicKeyPem) = NewSigner();
        var request = NewStageRequest(RandomNumberGenerator.GetBytes(1024));

        request.Signature = Sign(signer, request);

        // Signed by one key, verified against an unrelated trust anchor.
        Assert.False(AgentReleaseSignatureVerifier.Verify(request, otherPublicKeyPem));
    }

    // ---------------------------------------------------------------- key configuration

    [Fact]
    public void AnahtarYapilandirilmamissa_AcikHataDoner()
    {
        var signer = new AgentReleaseSigner(Options.Create(new AgentReleasePublishingOptions()));

        var ex = Assert.Throws<TOD.Platform.SharedKernel.Exceptions.BaseException>(() => signer.SignManifest([1, 2, 3]));
        Assert.Equal(AgentReleaseSigner.PrivateKeyNotConfiguredMessage, ex.Message);
    }

    [Fact]
    public void GecersizPemDosyasi_AnahtarIcerigiSizdirmadanHataDoner()
    {
        var path = Path.Combine(_tempDir, "broken.pem");
        File.WriteAllText(path, "-----BEGIN PRIVATE KEY-----\nnot-a-real-key\n-----END PRIVATE KEY-----");

        var signer = new AgentReleaseSigner(Options.Create(new AgentReleasePublishingOptions
        {
            SigningPrivateKeyPemPath = path
        }));

        var ex = Assert.Throws<TOD.Platform.SharedKernel.Exceptions.BaseException>(() => signer.SignManifest([1, 2, 3]));
        Assert.DoesNotContain("not-a-real-key", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TuretilenPublicKey_ImzayiDogrular()
    {
        // Operators provision the agent trust anchor from this exported key, so it must be the
        // counterpart of the key that actually signs.
        var (signer, _) = NewSigner();
        var request = NewStageRequest(RandomNumberGenerator.GetBytes(256));
        request.Signature = Sign(signer, request);

        Assert.True(AgentReleaseSignatureVerifier.Verify(request, signer.ExportPublicKeyPem()));
    }
}
