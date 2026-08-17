using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using STYS.Agent.Options;
using STYS.Agent.Services;
using TOD.Platform.SharedKernel.Exceptions;
using Xunit;

namespace STYS.Tests.Agent;

/// <summary>
/// Uploaded packages are attacker-influenced input. These pin that the hash/size come from the
/// bytes actually written, that no path component can escape the storage root, and that a failed
/// publish leaves nothing behind.
/// </summary>
public sealed class AgentReleasePackageStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "stys-release-storage", Guid.NewGuid().ToString("N"));
    private readonly AgentReleasePackageStorage _storage;

    public AgentReleasePackageStorageTests()
    {
        _storage = new AgentReleasePackageStorage(Options.Create(new AgentReleasePublishingOptions
        {
            StorageRootPath = _root
        }));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch
        {
            // Temp cleanup must not fail a test run.
        }
    }

    [Fact]
    public async Task TempYazma_HashVeBoyutuGercekBaytlardanHesaplar()
    {
        var bytes = RandomNumberGenerator.GetBytes(5000);

        var temp = await _storage.WriteTempAsync(new MemoryStream(bytes), 1024 * 1024, CancellationToken.None);

        Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)), temp.Sha256);
        Assert.Equal(bytes.LongLength, temp.Length);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(temp.Path));
    }

    [Fact]
    public async Task BoyutSiniriAsilirsa_HataVerirVeTempDosyaBirakmaz()
    {
        var bytes = RandomNumberGenerator.GetBytes(4096);

        await Assert.ThrowsAsync<BaseException>(() =>
            _storage.WriteTempAsync(new MemoryStream(bytes), maxBytes: 1024, CancellationToken.None));

        var incoming = Path.Combine(_root, ".incoming");
        var leftovers = Directory.Exists(incoming) ? Directory.GetFiles(incoming) : [];
        Assert.Empty(leftovers);
    }

    [Fact]
    public async Task BosPaket_Reddedilir()
    {
        await Assert.ThrowsAsync<BaseException>(() =>
            _storage.WriteTempAsync(new MemoryStream([]), 1024, CancellationToken.None));
    }

    [Fact]
    public async Task FinalTasima_BeklenenYerlesimiUretir()
    {
        var temp = await _storage.WriteTempAsync(new MemoryStream(RandomNumberGenerator.GetBytes(64)), 1024, CancellationToken.None);

        var finalPath = _storage.MoveToFinal(temp, kurumId: 7, releaseId: 12, version: "1.2.3", runtimeIdentifier: "win-x64");

        Assert.True(File.Exists(finalPath));
        Assert.False(File.Exists(temp.Path), "temp dosya tasindiktan sonra kalmamali");
        Assert.Equal(
            Path.Combine(Path.GetFullPath(_root), "7", "12", "win-x64", "stys-agent-1.2.3-win-x64.zip"),
            finalPath);
    }

    [Theory]
    [InlineData("../../escape")]
    [InlineData("..")]
    [InlineData("/etc/passwd")]
    [InlineData("C:\\Windows\\System32")]
    public async Task YolKacisiDenemesi_StorageRootDisinaCikamaz(string maliciousVersion)
    {
        var temp = await _storage.WriteTempAsync(new MemoryStream(RandomNumberGenerator.GetBytes(64)), 1024, CancellationToken.None);

        string? finalPath = null;
        try
        {
            finalPath = _storage.MoveToFinal(temp, kurumId: 1, releaseId: 1, version: maliciousVersion, runtimeIdentifier: "win-x64");
        }
        catch (BaseException)
        {
            // Rejecting outright is the preferred outcome.
            return;
        }

        // If sanitisation let it through, the result must still be inside the root.
        var normalizedRoot = Path.GetFullPath(_root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        Assert.StartsWith(normalizedRoot, Path.GetFullPath(finalPath!), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryDelete_VarOlmayanYolaSessizceDayanir()
    {
        _storage.TryDelete(Path.Combine(_root, "yok", "olmayan.zip"));
        _storage.TryDelete(null);

        var temp = await _storage.WriteTempAsync(new MemoryStream(RandomNumberGenerator.GetBytes(32)), 1024, CancellationToken.None);
        _storage.TryDelete(temp.Path);
        Assert.False(File.Exists(temp.Path));
    }
}
