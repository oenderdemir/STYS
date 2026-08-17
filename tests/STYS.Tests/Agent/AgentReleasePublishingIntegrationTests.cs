using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using STYS.Agent.Contracts.Dtos;
using STYS.Agent.Entities;
using STYS.Agent.Options;
using STYS.Agent.Services;
using STYS.Agent.Upgrade;
using STYS.Infrastructure.EntityFramework;
using STYS.Tests.TestSupport;
using TOD.Platform.SharedKernel.Exceptions;
using Xunit;

namespace STYS.Tests.Agent;

/// <summary>
/// Publish-path behaviour that only shows up against the real database: duplicate suppression via
/// the unique index, tenant isolation, and that a disabled or unsigned release is never handed to
/// an agent for staging.
/// </summary>
public sealed class AgentReleasePublishingIntegrationTests : IDisposable
{
    private readonly string _suffix = Guid.NewGuid().ToString("N")[..8];
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "stys-release-publish", Guid.NewGuid().ToString("N"));
    private readonly string _cs = Environment.GetEnvironmentVariable(IntegrationFactAttribute.ConnectionStringEnvVar) ?? string.Empty;

    private int _kurumId;
    private string _privateKeyPath = string.Empty;

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

    private async Task<bool> SetupAsync()
    {
        if (string.IsNullOrWhiteSpace(_cs))
        {
            return false;
        }

        Directory.CreateDirectory(_tempDir);

        // Ephemeral signing key: never a committed fixture.
        using var rsa = RSA.Create(3072);
        _privateKeyPath = Path.Combine(_tempDir, "signing.pem");
        await File.WriteAllTextAsync(_privateKeyPath, rsa.ExportPkcs8PrivateKeyPem());

        await using var db = AgentTestSupport.CreateDbContext(_cs);
        var (kurum, _, _) = await AgentTestSupport.SeedKurumIlTesisAsync(db, _suffix);
        _kurumId = kurum.Id;
        return true;
    }

    private DbContextFactoryForTest<StysAppDbContext> NewFactory() => new(() => AgentTestSupport.CreateDbContext(_cs));

    private AgentReleasePublishingService NewService(int? kurumId = null) => new(
        NewFactory(),
        new FakeKurumTenantAccessor(kurumId ?? _kurumId),
        new AgentReleaseSigner(Options.Create(new AgentReleasePublishingOptions { SigningPrivateKeyPemPath = _privateKeyPath })),
        new AgentReleasePackageStorage(Options.Create(new AgentReleasePublishingOptions { StorageRootPath = Path.Combine(_tempDir, "storage") })),
        Options.Create(new AgentReleasePublishingOptions
        {
            StorageRootPath = Path.Combine(_tempDir, "storage"),
            SigningPrivateKeyPemPath = _privateKeyPath
        }),
        Options.Create(new AgentCompatibilityOptions { SupportedContractVersion = "1.0.0" }));

    private static AgentReleasePublishRequest NewRequest(string version, bool enabled = true) => new()
    {
        Version = version,
        ContractVersion = "1.0.0",
        RuntimeIdentifier = "win-x64",
        ReleaseNotes = "integration test",
        Enabled = enabled
    };

    private static MemoryStream NewPackage(int size = 4096) => new(RandomNumberGenerator.GetBytes(size));

    // ---------------------------------------------------------------- A. publish computes hash/size

    [IntegrationFact]
    public async Task Publish_Sha256VePackageSizeSunucuTarafindaHesaplanir()
    {
        if (!await SetupAsync()) return;

        var bytes = RandomNumberGenerator.GetBytes(9001);
        var dto = await NewService().PublishAsync(NewRequest("1.0.1"), new MemoryStream(bytes), CancellationToken.None);

        Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)), dto.Sha256);
        Assert.Equal(bytes.LongLength, dto.PackageSize);
        Assert.False(string.IsNullOrWhiteSpace(dto.Signature));

        // The stored manifest signature must verify with the public half of the signing key.
        using var rsa = RSA.Create();
        rsa.ImportFromPem(await File.ReadAllTextAsync(_privateKeyPath));

        var stageRequest = new AgentStageUpgradeRequest
        {
            ReleaseId = dto.Id,
            Version = dto.Version,
            ContractVersion = dto.ContractVersion,
            RuntimeIdentifier = dto.RuntimeIdentifier,
            Sha256 = dto.Sha256,
            Signature = dto.Signature,
            PackageSize = dto.PackageSize,
            PublishedAt = dto.PublishedAt,
            ReleaseNotes = dto.ReleaseNotes
        };

        Assert.True(AgentReleaseSignatureVerifier.Verify(stageRequest, rsa.ExportSubjectPublicKeyInfoPem()));

        await CleanupAsync();
    }

    // ---------------------------------------------------------------- D. duplicate

    [IntegrationFact]
    public async Task Publish_AyniSurumIkinciKez_Reddedilir()
    {
        if (!await SetupAsync()) return;

        await NewService().PublishAsync(NewRequest("1.0.2"), NewPackage(), CancellationToken.None);

        var ex = await Assert.ThrowsAsync<BaseException>(() =>
            NewService().PublishAsync(NewRequest("1.0.2"), NewPackage(), CancellationToken.None));

        Assert.Equal(409, ex.ErrorCode);
        await CleanupAsync();
    }

    // ---------------------------------------------------------------- E. tenant isolation

    [IntegrationFact]
    public async Task BaskaKurumunReleaseine_Erisilemez()
    {
        if (!await SetupAsync()) return;

        var published = await NewService().PublishAsync(NewRequest("1.0.3"), NewPackage(), CancellationToken.None);

        // A tenant context for an unrelated kurum must not see or mutate it.
        var otherTenant = NewService(kurumId: _kurumId + 99_000);

        await Assert.ThrowsAsync<BaseException>(() => otherTenant.GetByIdAsync(published.Id, CancellationToken.None));
        await Assert.ThrowsAsync<BaseException>(() => otherTenant.SetEnabledAsync(published.Id, false, CancellationToken.None));
        Assert.DoesNotContain(await otherTenant.GetAllAsync(CancellationToken.None), x => x.Id == published.Id);

        await CleanupAsync();
    }

    // ---------------------------------------------------------------- F. selection

    [IntegrationFact]
    public async Task PasifRelease_StageIcinSecilmez_ImzasizRelease_DeSecilmez()
    {
        if (!await SetupAsync()) return;

        await using var db = AgentTestSupport.CreateDbContext(_cs);
        var agent = await AgentTestSupport.SeedAgentAsync(db, _kurumId, _suffix);
        agent.AgentVersion = "1.0.0";
        agent.RuntimeIdentifier = "win-x64";
        await db.SaveChangesAsync();

        // Published then disabled.
        var disabled = await NewService().PublishAsync(NewRequest("1.5.0"), NewPackage(), CancellationToken.None);
        await NewService().SetEnabledAsync(disabled.Id, enabled: false, CancellationToken.None);

        // Enabled but with the signature cleared, mimicking a publish that died before signing.
        var unsigned = await NewService().PublishAsync(NewRequest("1.6.0"), NewPackage(), CancellationToken.None);
        await using (var edit = AgentTestSupport.CreateDbContext(_cs))
        {
            var row = await edit.Set<AgentRelease>().FirstAsync(x => x.Id == unsigned.Id);
            row.Signature = string.Empty;
            await edit.SaveChangesAsync();
        }

        var releaseService = new AgentReleaseService(
            NewFactory(),
            new FakeKurumTenantAccessor(_kurumId),
            new FakeCurrentAgentContext { AgentId = agent.Id, KurumId = _kurumId },
            null!,
            Options.Create(new AgentCompatibilityOptions { SupportedContractVersion = "1.0.0" }));

        // No candidate remains, so staging must report "no suitable release" rather than pick one.
        var ex = await Assert.ThrowsAsync<BaseException>(() =>
            releaseService.StageUpgradeAsync(agent.Id, "test", CancellationToken.None));
        Assert.Equal(404, ex.ErrorCode);

        await CleanupAsync();
    }

    // ---------------------------------------------------------------- enable guard

    [IntegrationFact]
    public async Task ImzasizRelease_Aktiflestirilemez()
    {
        if (!await SetupAsync()) return;

        var published = await NewService().PublishAsync(NewRequest("1.7.0", enabled: false), NewPackage(), CancellationToken.None);

        await using (var edit = AgentTestSupport.CreateDbContext(_cs))
        {
            var row = await edit.Set<AgentRelease>().FirstAsync(x => x.Id == published.Id);
            row.Signature = string.Empty;
            await edit.SaveChangesAsync();
        }

        var ex = await Assert.ThrowsAsync<BaseException>(() =>
            NewService().SetEnabledAsync(published.Id, enabled: true, CancellationToken.None));
        Assert.Equal(409, ex.ErrorCode);

        await CleanupAsync();
    }

    private async Task CleanupAsync()
    {
        await using var db = AgentTestSupport.CreateDbContext(_cs);
        var releases = await db.Set<AgentRelease>().Where(x => x.KurumId == _kurumId).ToListAsync();
        db.Set<AgentRelease>().RemoveRange(releases);
        await db.SaveChangesAsync();
        await AgentTestSupport.CleanupAsync(db, _suffix);
    }
}
