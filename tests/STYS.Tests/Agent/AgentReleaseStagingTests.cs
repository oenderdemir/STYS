using System.Security.Cryptography;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using STYS.Agent.Client;
using STYS.Agent.Client.Commands;
using STYS.Agent.Client.Infrastructure;
using STYS.Agent.Client.Upgrade;
using STYS.Agent.Contracts.Dtos;
using STYS.Agent.Contracts.Enums;
using STYS.Agent.Contracts.Versioning;
using STYS.Agent.Entities;
using STYS.Agent.Options;
using STYS.Agent.Services;
using STYS.Agent.Upgrade;
using STYS.Infrastructure.EntityFramework;
using STYS.Tests.TestSupport;
using TOD.Platform.SharedKernel.Exceptions;
using AgentEntity = STYS.Agent.Entities.Agent;

namespace STYS.Tests.Agent;

public sealed class AgentReleaseStagingTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "stys-agent-release-tests", Guid.NewGuid().ToString("N"));

    public AgentReleaseStagingTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch
        {
        }
    }

    [Fact]
    public async Task ValidSignedPackage_Staged()
    {
        var manifest = CreateSignedManifest("1.2.0", "win-x64", "Paket sahnelenebilir.");
        var client = new DownloadClient(manifest.PackageBytes);
        var service = CreateStagingService(client, manifest.PublicKeyPem);

        var result = await service.StageAsync(CreateStageCommand(manifest), CancellationToken.None);
        var stagedFile = servicePaths.GetReleaseStagingPackagePath(manifest.ReleaseId.ToString(CultureInfo.InvariantCulture), manifest.RuntimeIdentifier);

        Assert.True(result.Success);
        Assert.True(File.Exists(stagedFile));
        Assert.Equal(AgentReleaseStageStatus.Staged, JsonSerializer.Deserialize<AgentStageUpgradeResponse>(result.ResultPayload!)!.StageStatus);
    }

    [Fact]
    public async Task TamperedPackage_Rejects()
    {
        var manifest = CreateSignedManifest("1.2.0", "win-x64", "Paket sahnelenebilir.");
        var tamperedBytes = manifest.PackageBytes.ToArray();
        tamperedBytes[0] ^= 0xFF;
        var tamperedClient = new DownloadClient(tamperedBytes);
        var service = CreateStagingService(tamperedClient, manifest.PublicKeyPem);

        var result = await service.StageAsync(CreateStageCommand(manifest), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("hash", result.ErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Fingerprint", await File.ReadAllTextAsync(serviceStorePath), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MissingPublicKey_Rejects()
    {
        var manifest = CreateSignedManifest("1.2.0", "win-x64", "Paket sahnelenebilir.");
        var service = CreateStagingService(new DownloadClient(manifest.PackageBytes), string.Empty);

        var result = await service.StageAsync(CreateStageCommand(manifest), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("imza", result.ErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WrongSignature_Rejects()
    {
        var manifest = CreateSignedManifest("1.2.0", "win-x64", "Paket sahnelenebilir.");
        var wrongKey = RSA.Create(2048);
        var wrongSignature = Convert.ToBase64String(wrongKey.SignData(
            AgentReleaseManifest.BuildSignaturePayload(manifest.Request),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pss));
        var badManifest = manifest with { Signature = wrongSignature };
        var service = CreateStagingService(new DownloadClient(manifest.PackageBytes), manifest.PublicKeyPem);

        var result = await service.StageAsync(CreateStageCommand(badManifest), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("imza", result.ErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HashMismatch_Rejects()
    {
        var manifest = CreateSignedManifest("1.2.0", "win-x64", "Paket sahnelenebilir.");
        var badManifest = manifest with { Sha256 = "DEADBEEF" };
        var service = CreateStagingService(new DownloadClient(manifest.PackageBytes), manifest.PublicKeyPem);

        var result = await service.StageAsync(CreateStageCommand(badManifest), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("hash", result.ErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PartialDownload_FinalPackageOlusmaz()
    {
        var manifest = CreateSignedManifest("1.2.0", "win-x64", "Paket sahnelenebilir.");
        var client = new ThrowingDownloadClient(new InvalidOperationException("download failed"));
        var service = CreateStagingService(client, manifest.PublicKeyPem);

        var result = await service.StageAsync(CreateStageCommand(manifest), CancellationToken.None);
        var stagedFile = servicePaths.GetReleaseStagingPackagePath(manifest.ReleaseId.ToString(CultureInfo.InvariantCulture), manifest.RuntimeIdentifier);

        Assert.False(result.Success);
        Assert.False(File.Exists(stagedFile));
    }

    [Fact]
    public async Task StagingMetadata_SecretsIcermiyor()
    {
        var manifest = CreateSignedManifest("1.2.0", "win-x64", "Paket sahnelenebilir.");
        var service = CreateStagingService(new DownloadClient(manifest.PackageBytes), manifest.PublicKeyPem);

        await service.StageAsync(CreateStageCommand(manifest), CancellationToken.None);

        var rawJson = await File.ReadAllTextAsync(serviceStorePath);
        Assert.DoesNotContain("Fingerprint", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TargetFingerprint", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ClientSecret", rawJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AbsolutePathEntry_Rejects()
    {
        var zipPath = CreateZipArchive(entries =>
        {
            var entry = entries.CreateEntry(Path.DirectorySeparatorChar == '\\' ? @"C:\evil.txt" : "/tmp/evil.txt");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("evil");
        });

        var extractDir = Path.Combine(_tempDir, "abs");
        var ex = Assert.Throws<InvalidOperationException>(() => AgentPackageExtractionGuard.ExtractPackage(zipPath, extractDir));
        Assert.Contains("Kök yol", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TraversalEntry_Rejects()
    {
        var zipPath = CreateZipArchive(entries =>
        {
            var entry = entries.CreateEntry("../extract-evil/payload.txt");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("evil");
        });

        var extractDir = Path.Combine(_tempDir, "traversal");
        var ex = Assert.Throws<InvalidOperationException>(() => AgentPackageExtractionGuard.ExtractPackage(zipPath, extractDir));
        Assert.Contains("Güvensiz", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SymlinkEntry_Rejects()
    {
        var zipPath = CreateZipArchive(entries =>
        {
            var entry = entries.CreateEntry("link");
            entry.ExternalAttributes = unchecked((int)0xA0000000);
        });

        var extractDir = Path.Combine(_tempDir, "symlink");
        var ex = Assert.Throws<InvalidOperationException>(() => AgentPackageExtractionGuard.ExtractPackage(zipPath, extractDir));
        Assert.Contains("Symlink", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WrongRid_Rejects()
    {
        var (releaseService, agentId, _, _) = await CreateBackendReleaseServiceAsync(agentRuntimeIdentifier: "linux-x64");

        await Assert.ThrowsAsync<BaseException>(() => releaseService.StageUpgradeAsync(agentId, "tester", CancellationToken.None));
    }

    [Fact]
    public async Task IncompatibleContract_Rejects()
    {
        var (releaseService, agentId, _, _) = await CreateBackendReleaseServiceAsync(contractVersion: "2.0.0");

        await Assert.ThrowsAsync<BaseException>(() => releaseService.StageUpgradeAsync(agentId, "tester", CancellationToken.None));
    }

    [Fact]
    public async Task Downgrade_Rejects()
    {
        var (releaseService, agentId, _, _) = await CreateBackendReleaseServiceAsync(releaseVersion: "0.9.0");

        await Assert.ThrowsAsync<BaseException>(() => releaseService.StageUpgradeAsync(agentId, "tester", CancellationToken.None));
    }

    [Fact]
    public async Task CrossTenantRelease_Rejects()
    {
        var (releaseService, agentId, _, _) = await CreateBackendReleaseServiceAsync(agentKurumId: 100, releaseKurumId: 200);

        await Assert.ThrowsAsync<BaseException>(() => releaseService.StageUpgradeAsync(agentId, "tester", CancellationToken.None));
    }

    [Fact]
    public async Task DuplicateActiveStage_TekCommand()
    {
        var (releaseService, agentId, _, dbName) = await CreateBackendReleaseServiceAsync();
        var first = await releaseService.StageUpgradeAsync(agentId, "tester", CancellationToken.None);
        var second = await releaseService.StageUpgradeAsync(agentId, "tester", CancellationToken.None);

        Assert.Equal(first.Id, second.Id);

        await using var verifyDb = CreateBackendDbContext(dbName, 100, isSuperAdmin: false);
        Assert.Single(await verifyDb.Set<AgentCommand>().Where(x => x.AgentId == agentId && !x.IsDeleted).ToListAsync());
    }

    [Fact]
    public async Task StagePayload_UrlIcermiyor()
    {
        var (releaseService, agentId, _, _) = await CreateBackendReleaseServiceAsync();
        var command = await releaseService.StageUpgradeAsync(agentId, "tester", CancellationToken.None);

        Assert.DoesNotContain("http://", command.Payload ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://", command.Payload ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("file://", command.Payload ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Download_SameAgentAndCorrectRelease_Basarili()
    {
        var (releaseService, agentId, releaseId, _) = await CreateBackendReleaseServiceAsync();
        await releaseService.StageUpgradeAsync(agentId, "tester", CancellationToken.None);

        var (release, bytes) = await releaseService.GetReleasePackageAsync(releaseId, CancellationToken.None);

        Assert.Equal(releaseId, release.Id);
        Assert.NotEmpty(bytes);
    }

    [Fact]
    public async Task Download_SameKurumBaskaAgent_Rejects()
    {
        var (releaseService, agentId, releaseId, dbName) = await CreateBackendReleaseServiceAsync();
        await releaseService.StageUpgradeAsync(agentId, "tester", CancellationToken.None);

        await using var db = CreateBackendDbContext(dbName, 100, false);
        var otherAgent = await SeedDownloadAgentAsync(db, 100, "other-agent");
        var otherService = CreateReleaseService(dbName, 100, otherAgent.Id, 100);

        await Assert.ThrowsAsync<BaseException>(() => otherService.GetReleasePackageAsync(releaseId, CancellationToken.None));
    }

    [Fact]
    public async Task Download_WrongRid_Rejects()
    {
        var (releaseService, agentId, releaseId, dbName) = await CreateBackendReleaseServiceAsync();
        await releaseService.StageUpgradeAsync(agentId, "tester", CancellationToken.None);

        await using var db = CreateBackendDbContext(dbName, 100, false);
        var release = await db.Set<AgentRelease>().FirstAsync(x => x.Id == releaseId);
        release.RuntimeIdentifier = "linux-x64";
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<BaseException>(() => releaseService.GetReleasePackageAsync(releaseId, CancellationToken.None));
    }

    [Fact]
    public async Task Download_WrongContract_Rejects()
    {
        var (releaseService, agentId, releaseId, dbName) = await CreateBackendReleaseServiceAsync();
        await releaseService.StageUpgradeAsync(agentId, "tester", CancellationToken.None);

        await using var db = CreateBackendDbContext(dbName, 100, false);
        var release = await db.Set<AgentRelease>().FirstAsync(x => x.Id == releaseId);
        release.ContractVersion = "2.0.0";
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<BaseException>(() => releaseService.GetReleasePackageAsync(releaseId, CancellationToken.None));
    }

    [Fact]
    public async Task Download_DowngradeOrCurrentVersion_Rejects()
    {
        var (releaseService, agentId, releaseId, dbName) = await CreateBackendReleaseServiceAsync();
        await releaseService.StageUpgradeAsync(agentId, "tester", CancellationToken.None);

        await using var db = CreateBackendDbContext(dbName, 100, false);
        var release = await db.Set<AgentRelease>().FirstAsync(x => x.Id == releaseId);
        release.Version = "1.0.0";
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<BaseException>(() => releaseService.GetReleasePackageAsync(releaseId, CancellationToken.None));
    }

    [Fact]
    public async Task Download_DisabledRelease_Rejects()
    {
        var (releaseService, agentId, releaseId, dbName) = await CreateBackendReleaseServiceAsync();
        await releaseService.StageUpgradeAsync(agentId, "tester", CancellationToken.None);

        await using var db = CreateBackendDbContext(dbName, 100, false);
        var release = await db.Set<AgentRelease>().FirstAsync(x => x.Id == releaseId);
        release.Enabled = false;
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<BaseException>(() => releaseService.GetReleasePackageAsync(releaseId, CancellationToken.None));
    }

    [Fact]
    public async Task Download_NoActiveStageCommand_Rejects()
    {
        var (releaseService, _, releaseId, _) = await CreateBackendReleaseServiceAsync();

        await Assert.ThrowsAsync<BaseException>(() => releaseService.GetReleasePackageAsync(releaseId, CancellationToken.None));
    }

    [Fact]
    public async Task Download_CommandManifestMismatch_Rejects()
    {
        var (releaseService, agentId, releaseId, dbName) = await CreateBackendReleaseServiceAsync();
        await releaseService.StageUpgradeAsync(agentId, "tester", CancellationToken.None);

        await using var db = CreateBackendDbContext(dbName, 100, false);
        var command = await db.Set<AgentCommand>().FirstAsync(x => x.AgentId == agentId && x.ReleaseId == releaseId && x.CommandType == "AgentStageUpgrade");
        var payload = JsonSerializer.Deserialize<AgentStageUpgradeRequest>(command.Payload!)!;
        payload.Sha256 = "BAD-SHA";
        command.Payload = JsonSerializer.Serialize(payload);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<BaseException>(() => releaseService.GetReleasePackageAsync(releaseId, CancellationToken.None));
    }

    [Fact]
    public async Task Download_CrossTenant_Rejects()
    {
        var (releaseService, agentId, releaseId, dbName) = await CreateBackendReleaseServiceAsync();
        await releaseService.StageUpgradeAsync(agentId, "tester", CancellationToken.None);

        await using var db = CreateBackendDbContext(dbName, 200, true);
        var tenantTwoAgent = await SeedDownloadAgentAsync(db, 200, "tenant-two");
        var otherService = CreateReleaseService(dbName, 200, tenantTwoAgent.Id, 200);

        await Assert.ThrowsAsync<BaseException>(() => otherService.GetReleasePackageAsync(releaseId, CancellationToken.None));
    }

    private (int ReleaseId, string Version, string RuntimeIdentifier, string Sha256, string Signature, long PackageSize, string PublicKeyPem, byte[] PackageBytes, AgentStageUpgradeRequest Request) CreateSignedManifest(string version, string runtimeIdentifier, string releaseNotes)
    {
        using var rsa = RSA.Create(2048);
        var releaseId = Random.Shared.Next(1, int.MaxValue);
        var packageBytes = Encoding.UTF8.GetBytes($"package::{Guid.NewGuid():N}");
        var request = new AgentStageUpgradeRequest
        {
            ReleaseId = releaseId,
            Version = version,
            ContractVersion = "1.0.0",
            RuntimeIdentifier = runtimeIdentifier,
            Sha256 = Convert.ToHexString(SHA256.HashData(packageBytes)),
            PackageSize = packageBytes.LongLength,
            PublishedAt = DateTimeOffset.UtcNow,
            ReleaseNotes = releaseNotes
        };
        request.Signature = Convert.ToBase64String(rsa.SignData(
            AgentReleaseManifest.BuildSignaturePayload(request),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pss));

        return (
            request.ReleaseId,
            request.Version,
            request.RuntimeIdentifier,
            request.Sha256,
            request.Signature,
            request.PackageSize,
            rsa.ExportSubjectPublicKeyInfoPem(),
            packageBytes,
            request);
    }

    private AgentStageUpgradeCommand CreateStageCommand((int ReleaseId, string Version, string RuntimeIdentifier, string Sha256, string Signature, long PackageSize, string PublicKeyPem, byte[] PackageBytes, AgentStageUpgradeRequest Request) manifest) =>
        new()
        {
            ReleaseId = manifest.ReleaseId,
            Version = manifest.Version,
            ContractVersion = manifest.Request.ContractVersion,
            RuntimeIdentifier = manifest.RuntimeIdentifier,
            Sha256 = manifest.Sha256,
            Signature = manifest.Signature,
            PackageSize = manifest.PackageSize,
            PublishedAt = manifest.Request.PublishedAt,
            ReleaseNotes = manifest.Request.ReleaseNotes
        };

    private AgentReleaseStagingService CreateStagingService(DownloadClient client, string publicKeyPem)
    {
        return CreateStagingService((IStysAgentApiClient)client, publicKeyPem);
    }

    private AgentReleaseStagingService CreateStagingService(IStysAgentApiClient client, string publicKeyPem)
    {
        servicePaths = new TempAgentPathResolver(_tempDir);
        serviceStorePath = Path.Combine(servicePaths.ReleaseStagingRootDirectory, "release-staging.json");
        return new AgentReleaseStagingService(
            client,
            servicePaths,
            new FileAgentReleaseStagingStore(servicePaths, NullLogger<FileAgentReleaseStagingStore>.Instance),
            Options.Create(new AgentUpgradeOptions { ReleasePublicKeyPem = publicKeyPem }),
            NullLogger<AgentReleaseStagingService>.Instance);
    }

    private async Task<(AgentReleaseService releaseService, int agentId, int releaseId, string dbName)> CreateBackendReleaseServiceAsync(
        string releaseVersion = "1.2.0",
        string contractVersion = "1.0.0",
        string agentRuntimeIdentifier = "win-x64",
        string releaseRuntimeIdentifier = "win-x64",
        int agentKurumId = 100,
        int releaseKurumId = 100)
    {
        var dbName = Guid.NewGuid().ToString("N");
        var db = CreateBackendDbContext(dbName, agentKurumId, false);
        var (agentId, releaseId) = await SeedBackendAsync(dbName, db, agentKurumId, releaseKurumId, agentRuntimeIdentifier, releaseRuntimeIdentifier, releaseVersion, contractVersion);
        return (CreateReleaseService(dbName, agentKurumId, agentId, agentKurumId), agentId, releaseId, dbName);
    }

    private AgentReleaseService CreateReleaseService(string dbName, int tenantKurumId, int currentAgentId, int currentAgentKurumId)
    {
        var tenantAccessor = new FakeKurumTenantAccessor(tenantKurumId);
        var currentAgentContext = new FakeCurrentAgentContext
        {
            AgentId = currentAgentId,
            KurumId = currentAgentKurumId,
            IsAuthenticated = true
        };
        var factory = new DbContextFactoryForTest<StysAppDbContext>(() => CreateBackendDbContext(dbName, tenantKurumId, false));
        var commandService = new AgentCommandService(factory, tenantAccessor, NullLogger<AgentCommandService>.Instance, compatibilityOptions: Options.Create(new AgentCompatibilityOptions()));
        return new AgentReleaseService(factory, tenantAccessor, currentAgentContext, commandService, Options.Create(new AgentCompatibilityOptions()));
    }

    private static StysAppDbContext CreateBackendDbContext(string dbName, int currentKurumId, bool isSuperAdmin)
    {
        var options = new DbContextOptionsBuilder<StysAppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        if (isSuperAdmin)
        {
            return new StysAppDbContext(options, currentTenantAccessor: new FakeSuperAdminTenantAccessor());
        }

        return new StysAppDbContext(options, currentTenantAccessor: new FakeKurumTenantAccessor(currentKurumId));
    }

    private static async Task<(int AgentId, int ReleaseId)> SeedBackendAsync(
        string dbName,
        StysAppDbContext db,
        int agentKurumId,
        int releaseKurumId,
        string agentRuntimeIdentifier,
        string releaseRuntimeIdentifier,
        string releaseVersion,
        string contractVersion)
    {
        var releaseBytes = Encoding.UTF8.GetBytes($"release::{releaseVersion}::{releaseRuntimeIdentifier}");
        var releasePath = Path.Combine(Path.GetTempPath(), $"stys-release-{Guid.NewGuid():N}.bin");
        await File.WriteAllBytesAsync(releasePath, releaseBytes);

        var agent = new AgentEntity
        {
            Ad = "Agent-Release",
            AgentKey = $"AGNT-{Guid.NewGuid():N}"[..16],
            KurumId = agentKurumId,
            Durum = AgentDurum.Active,
            AgentVersion = "1.0.0",
            ContractVersion = "1.0.0",
            RuntimeIdentifier = agentRuntimeIdentifier,
            CreatedBy = "test",
            CreatedAt = DateTime.UtcNow
        };
        db.Set<AgentEntity>().Add(agent);
        await db.SaveChangesAsync();
        db.Set<AgentScope>().Add(new AgentScope
        {
            AgentId = agent.Id,
            KurumId = agentKurumId,
            Scope = "agent.command.execute",
            AktifMi = true,
            CreatedBy = "test",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var releaseContext = releaseKurumId == agentKurumId
            ? db
            : CreateBackendDbContext(dbName, releaseKurumId, isSuperAdmin: true);

        releaseContext.AllowExplicitTenantWritesWithoutAmbientTenant = true;
        var release = new AgentRelease
        {
            KurumId = releaseKurumId,
            Version = releaseVersion,
            ContractVersion = contractVersion,
            RuntimeIdentifier = releaseRuntimeIdentifier,
            Sha256 = Convert.ToHexString(SHA256.HashData(releaseBytes)),
            Signature = "SIG",
            PackageSize = releaseBytes.LongLength,
            PublishedAt = DateTimeOffset.UtcNow,
            Enabled = true,
            ReleaseNotes = "Test release",
            PackagePath = releasePath,
            CreatedBy = "test",
            CreatedAt = DateTime.UtcNow
        };
        releaseContext.Set<AgentRelease>().Add(release);
        await releaseContext.SaveChangesAsync();
        releaseContext.AllowExplicitTenantWritesWithoutAmbientTenant = false;
        return (agent.Id, release.Id);
    }

    private static async Task<AgentEntity> SeedDownloadAgentAsync(StysAppDbContext db, int kurumId, string suffix)
    {
        db.AllowExplicitTenantWritesWithoutAmbientTenant = true;
        var agent = new AgentEntity
        {
            Ad = $"Agent-{suffix}",
            AgentKey = $"AGNT-{Guid.NewGuid():N}"[..16],
            KurumId = kurumId,
            Durum = AgentDurum.Active,
            AgentVersion = "1.0.0",
            ContractVersion = "1.0.0",
            RuntimeIdentifier = "win-x64",
            CreatedBy = "test",
            CreatedAt = DateTime.UtcNow
        };

        db.Set<AgentEntity>().Add(agent);
        await db.SaveChangesAsync();
        db.Set<AgentScope>().Add(new AgentScope
        {
            AgentId = agent.Id,
            KurumId = kurumId,
            Scope = "agent.command.execute",
            AktifMi = true,
            CreatedBy = "test",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        db.AllowExplicitTenantWritesWithoutAmbientTenant = false;
        return agent;
    }

    private sealed class TempAgentPathResolver : IAgentPathResolver
    {
        public TempAgentPathResolver(string root)
        {
            DataDirectory = root;
            SharedDataDirectory = root;
            UpdaterPrivateDataDirectory = Path.Combine(root, "updater-private");
        }

        public string DataDirectory { get; }
        public string SharedDataDirectory { get; }
        public string UpdaterPrivateDataDirectory { get; }
        public string LogDirectory => Path.Combine(DataDirectory, "logs");
        public string BootstrapConfigurationPath => Path.Combine(DataDirectory, "bootstrap.json");
        public string CredentialStorePath => Path.Combine(DataDirectory, "credential.dat");
        public string LocalDevicesStorePath => Path.Combine(DataDirectory, "local-devices.json");
        public string LocalDeviceTerminalsStorePath => Path.Combine(DataDirectory, "local-device-terminals.json");
        public string PavoPairingStorePath => Path.Combine(DataDirectory, "pavo-pairing.dat");
        public string AgentCommandExecutionStorePath => Path.Combine(DataDirectory, "agent-command-executions.json");
        public string InstanceIdPath => Path.Combine(DataDirectory, "instance.id");
        public string ReleaseStagingRootDirectory => Path.Combine(SharedDataDirectory, "updates", "staging");
        public string UpgradeBackupRootDirectory => Path.Combine(UpdaterPrivateDataDirectory, "updates", "backup");
        public string UpgradeExtractRootDirectory => Path.Combine(UpdaterPrivateDataDirectory, "updates", "extract");
        public string UpgradeTempRootDirectory => Path.Combine(UpdaterPrivateDataDirectory, "updates", "temp");
        public string GetReleaseStagingDirectory(string version, string runtimeIdentifier) => Path.Combine(ReleaseStagingRootDirectory, version, runtimeIdentifier);
        public string GetReleaseStagingStatePath(string version, string runtimeIdentifier) => Path.Combine(GetReleaseStagingDirectory(version, runtimeIdentifier), "staging-state.json");
        public string GetReleaseStagingPackagePath(string version, string runtimeIdentifier) => Path.Combine(GetReleaseStagingDirectory(version, runtimeIdentifier), "package.bin");
    }

    private sealed class DownloadClient : IStysAgentApiClient
    {
        private readonly byte[] _bytes;

        public DownloadClient(byte[] bytes) => _bytes = bytes;

        public Task<byte[]> DownloadReleasePackageAsync(int releaseId, CancellationToken cancellationToken) =>
            Task.FromResult(_bytes);

        public Task<AgentEnrollmentResponse> EnrollAsync(AgentEnrollmentRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AgentTokenResponse> GetTokenAsync(AgentTokenRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task SendHeartbeatAsync(AgentHeartbeatRequest request, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<AgentConfigDto?> GetConfigurationAsync(long currentVersion, CancellationToken cancellationToken) => Task.FromResult<AgentConfigDto?>(null);
        public Task<AgentSelfDto> GetMeAsync(CancellationToken cancellationToken) => Task.FromResult(new AgentSelfDto());
        public Task<AgentPavoDeviceRegistrationResult> RegisterPavoDeviceAsync(AgentPavoDeviceRegisterRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AgentPavoDeviceStatusSnapshotDto?> GetPavoDeviceStatusSnapshotAsync(AgentPavoDeviceStatusSnapshotRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<AgentCommandDto>> GetPendingCommandsAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<AgentCommandDto>>([]);
        public Task AcceptCommandAsync(Guid commandId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SetRunningCommandAsync(Guid commandId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task CompleteCommandAsync(Guid commandId, AgentCommandCompleteRequest request, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task FailCommandAsync(Guid commandId, AgentCommandCompleteRequest request, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task RejectCommandAsync(Guid commandId, AgentCommandCompleteRequest request, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class ThrowingDownloadClient : IStysAgentApiClient
    {
        private readonly Exception _exception;

        public ThrowingDownloadClient(Exception exception) => _exception = exception;
        public Task<byte[]> DownloadReleasePackageAsync(int releaseId, CancellationToken cancellationToken) => throw _exception;
        public Task<AgentEnrollmentResponse> EnrollAsync(AgentEnrollmentRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AgentTokenResponse> GetTokenAsync(AgentTokenRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task SendHeartbeatAsync(AgentHeartbeatRequest request, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<AgentConfigDto?> GetConfigurationAsync(long currentVersion, CancellationToken cancellationToken) => Task.FromResult<AgentConfigDto?>(null);
        public Task<AgentSelfDto> GetMeAsync(CancellationToken cancellationToken) => Task.FromResult(new AgentSelfDto());
        public Task<AgentPavoDeviceRegistrationResult> RegisterPavoDeviceAsync(AgentPavoDeviceRegisterRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AgentPavoDeviceStatusSnapshotDto?> GetPavoDeviceStatusSnapshotAsync(AgentPavoDeviceStatusSnapshotRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<AgentCommandDto>> GetPendingCommandsAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<AgentCommandDto>>([]);
        public Task AcceptCommandAsync(Guid commandId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SetRunningCommandAsync(Guid commandId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task CompleteCommandAsync(Guid commandId, AgentCommandCompleteRequest request, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task FailCommandAsync(Guid commandId, AgentCommandCompleteRequest request, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task RejectCommandAsync(Guid commandId, AgentCommandCompleteRequest request, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private TempAgentPathResolver servicePaths = null!;
    private string serviceStorePath = string.Empty;

    private string CreateZipArchive(Action<ZipArchive> writer)
    {
        var zipPath = Path.Combine(_tempDir, $"{Guid.NewGuid():N}.zip");
        using (var stream = File.Create(zipPath))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false))
        {
            writer(archive);
        }

        return zipPath;
    }
}
