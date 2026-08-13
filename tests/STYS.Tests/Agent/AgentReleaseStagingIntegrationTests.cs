using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using STYS.Agent.Contracts.Dtos;
using STYS.Agent.Entities;
using STYS.Agent.Options;
using STYS.Agent.Services;
using STYS.Infrastructure.EntityFramework;
using STYS.Tests.TestSupport;
using TOD.Platform.SharedKernel.Exceptions;
using Xunit;
using AgentEntity = STYS.Agent.Entities.Agent;

namespace STYS.Tests.Agent;

[Collection(SqlServerIntegrationCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Domain", "Agent")]
public sealed class AgentReleaseStagingIntegrationTests : IAsyncLifetime
{
    private const string TestMarker = "agent-release-stage";
    private string? _connectionString;
    private string _suffix = string.Empty;

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    [IntegrationFact]
    public async Task ConcurrentStageUpgrade_OnlyOneActiveCommandOlusur()
    {
        await using var db = await SetupAsync();
        if (db is null) return;

        var (agentKurumId, agentId, releaseId) = await SeedAsync(db, agentRuntimeIdentifier: "win-x64", releaseRuntimeIdentifier: "win-x64", releaseVersion: "1.2.0", contractVersion: "1.0.0");
        var firstService = CreateReleaseService(agentId, agentKurumId);
        var secondService = CreateReleaseService(agentId, agentKurumId);

        var firstTask = firstService.StageUpgradeAsync(agentId, "tester", CancellationToken.None);
        var secondTask = secondService.StageUpgradeAsync(agentId, "tester", CancellationToken.None);
        await Task.WhenAll(firstTask, secondTask);

        Assert.Equal(firstTask.Result.Id, secondTask.Result.Id);
        await using var verifyDb = AgentTestSupport.CreateDbContext(_connectionString!);
        Assert.Single(await verifyDb.Set<AgentCommand>().Where(x => x.AgentId == agentId && x.ReleaseId == releaseId && x.CommandType == "AgentStageUpgrade" && !x.IsDeleted).ToListAsync());

        await CleanupAsync(db);
    }

    [IntegrationFact]
    public async Task ExpiredStageUpgrade_YeniCommandAcabilir()
    {
        await using var db = await SetupAsync();
        if (db is null) return;

        var (agentKurumId, agentId, releaseId) = await SeedAsync(db, agentRuntimeIdentifier: "win-x64", releaseRuntimeIdentifier: "win-x64", releaseVersion: "1.2.0", contractVersion: "1.0.0");
        var service = CreateReleaseService(agentId, agentKurumId);

        var expired = new AgentCommand
        {
            Id = Guid.NewGuid(),
            AgentId = agentId,
            KurumId = agentKurumId,
            ReleaseId = releaseId,
            CommandType = "AgentStageUpgrade",
            Payload = BuildStagePayload(releaseId, "1.2.0", "win-x64", _suffix),
            Status = STYS.Agent.Contracts.Enums.AgentCommandStatus.Expired,
            CreatedBy = "test",
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-5)
        };
        db.Set<AgentCommand>().Add(expired);
        await db.SaveChangesAsync();

        var result = await service.StageUpgradeAsync(agentId, "tester", CancellationToken.None);
        Assert.NotEqual(expired.Id, result.Id);

        await using var verifyDb = AgentTestSupport.CreateDbContext(_connectionString!);
        Assert.Equal(2, await verifyDb.Set<AgentCommand>().CountAsync(x => x.AgentId == agentId && x.ReleaseId == releaseId && x.CommandType == "AgentStageUpgrade" && !x.IsDeleted));

        await CleanupAsync(db);
    }

    [IntegrationFact]
    public async Task ExactReleaseId_PackageDownloadCalisir()
    {
        await using var db = await SetupAsync();
        if (db is null) return;

        var (agentKurumId, agentId, releaseId) = await SeedAsync(db, agentRuntimeIdentifier: "win-x64", releaseRuntimeIdentifier: "win-x64", releaseVersion: "1.2.0", contractVersion: "1.0.0");
        var service = CreateReleaseService(agentId, agentKurumId);

        var (release, bytes) = await service.GetReleasePackageAsync(releaseId, CancellationToken.None);

        Assert.Equal(releaseId, release.Id);
        Assert.NotEmpty(bytes);

        await CleanupAsync(db);
    }

    [IntegrationFact]
    public async Task CrossTenantReleaseId_Rejects()
    {
        await using var db = await SetupAsync();
        if (db is null) return;

        var (agentKurumId, agentId, _) = await SeedAsync(db, agentRuntimeIdentifier: "win-x64", releaseRuntimeIdentifier: "win-x64", releaseVersion: "1.2.0", contractVersion: "1.0.0", separateReleaseKurum: true);
        var service = CreateReleaseService(agentId, agentKurumId);

        await Assert.ThrowsAsync<BaseException>(() => service.StageUpgradeAsync(agentId, "tester", CancellationToken.None));

        await CleanupAsync(db);
    }

    [IntegrationFact]
    public async Task DisabledReleaseId_Rejects()
    {
        await using var db = await SetupAsync();
        if (db is null) return;

        var (agentKurumId, agentId, releaseId) = await SeedAsync(db, agentRuntimeIdentifier: "win-x64", releaseRuntimeIdentifier: "win-x64", releaseVersion: "1.2.0", contractVersion: "1.0.0");
        var release = await db.Set<AgentRelease>().FirstAsync(x => x.Id == releaseId);
        release.Enabled = false;
        await db.SaveChangesAsync();

        var service = CreateReleaseService(agentId, agentKurumId);
        await Assert.ThrowsAsync<BaseException>(() => service.StageUpgradeAsync(agentId, "tester", CancellationToken.None));

        await CleanupAsync(db);
    }

    [IntegrationFact]
    public async Task WrongRidOrContract_Rejects()
    {
        await using var db = await SetupAsync();
        if (db is null) return;

        var (agentKurumId, agentId, _) = await SeedAsync(db, agentRuntimeIdentifier: "linux-x64", releaseRuntimeIdentifier: "win-x64", releaseVersion: "1.2.0", contractVersion: "2.0.0");
        var service = CreateReleaseService(agentId, agentKurumId);

        await Assert.ThrowsAsync<BaseException>(() => service.StageUpgradeAsync(agentId, "tester", CancellationToken.None));

        await CleanupAsync(db);
    }

    private async Task<StysAppDbContext?> SetupAsync()
    {
        _connectionString = Environment.GetEnvironmentVariable(IntegrationFactAttribute.ConnectionStringEnvVar);
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return null;
        }

        _suffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];
        return AgentTestSupport.CreateDbContext(_connectionString);
    }

    private AgentReleaseService CreateReleaseService(int agentId, int agentKurumId) =>
        new(
            new DbContextFactoryForTest<StysAppDbContext>(() => AgentTestSupport.CreateDbContext(_connectionString!)),
            new FakeKurumTenantAccessor(agentKurumId),
            new FakeCurrentAgentContext
            {
                AgentId = agentId,
                KurumId = agentKurumId,
                IsAuthenticated = true
            },
            new AgentCommandService(
                new DbContextFactoryForTest<StysAppDbContext>(() => AgentTestSupport.CreateDbContext(_connectionString!)),
                new FakeKurumTenantAccessor(agentKurumId),
                NullLogger<AgentCommandService>.Instance,
                compatibilityOptions: Options.Create(new AgentCompatibilityOptions { SupportedContractVersion = "1.0.0" })),
            Options.Create(new AgentCompatibilityOptions { SupportedContractVersion = "1.0.0" }));

    private async Task<(int AgentKurumId, int AgentId, int ReleaseId)> SeedAsync(
        StysAppDbContext db,
        string agentRuntimeIdentifier,
        string releaseRuntimeIdentifier,
        string releaseVersion,
        string contractVersion,
        bool separateReleaseKurum = false)
    {
        var (agentKurum, _, _) = await AgentTestSupport.SeedKurumIlTesisAsync(db, _suffix);
        var agentKurumId = agentKurum.Id;

        var agent = await AgentTestSupport.SeedAgentAsync(db, agentKurumId, _suffix);
        agent.AgentVersion = "1.0.0";
        agent.ContractVersion = "1.0.0";
        agent.RuntimeIdentifier = agentRuntimeIdentifier;
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

        var releaseBytes = Encoding.UTF8.GetBytes($"release::{releaseVersion}::{releaseRuntimeIdentifier}::{_suffix}");
        var releasePath = Path.Combine(Path.GetTempPath(), $"stys-release-{_suffix}.bin");
        await File.WriteAllBytesAsync(releasePath, releaseBytes);

        var releaseKurumId = agentKurumId;
        StysAppDbContext? releaseDb = null;
        if (separateReleaseKurum)
        {
            releaseDb = AgentTestSupport.CreateDbContext(_connectionString!);
            var (releaseKurum, _, _) = await AgentTestSupport.SeedKurumIlTesisAsync(releaseDb, $"{_suffix}-release");
            releaseKurumId = releaseKurum.Id;
        }

        try
        {
            var context = releaseDb ?? db;
            context.AllowExplicitTenantWritesWithoutAmbientTenant = true;
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
                ReleaseNotes = $"Test release {_suffix}",
                PackagePath = releasePath,
                CreatedBy = "test",
                CreatedAt = DateTime.UtcNow
            };
            context.Set<AgentRelease>().Add(release);
            await context.SaveChangesAsync();
            context.AllowExplicitTenantWritesWithoutAmbientTenant = false;
            return (agentKurumId, agent.Id, release.Id);
        }
        finally
        {
            if (releaseDb is not null)
            {
                await releaseDb.DisposeAsync();
            }
        }
    }

    private async Task CleanupAsync(StysAppDbContext db)
    {
        var releases = await db.Set<AgentRelease>().Where(x => x.ReleaseNotes != null && x.ReleaseNotes.Contains(_suffix)).ToListAsync();
        db.Set<AgentRelease>().RemoveRange(releases);

        var commands = await db.Set<AgentCommand>().Where(x => x.CommandType == "AgentStageUpgrade" && x.Payload != null && x.Payload.Contains(_suffix)).ToListAsync();
        db.Set<AgentCommand>().RemoveRange(commands);

        await db.SaveChangesAsync();
        await AgentTestSupport.CleanupAsync(db, _suffix);
    }

    private static string BuildStagePayload(int releaseId, string version, string runtimeIdentifier, string releaseNotes) =>
        System.Text.Json.JsonSerializer.Serialize(new AgentStageUpgradeRequest
        {
            ReleaseId = releaseId,
            Version = version,
            ContractVersion = "1.0.0",
            RuntimeIdentifier = runtimeIdentifier,
            Sha256 = "SHA",
            Signature = "SIG",
            PackageSize = 0,
            PublishedAt = DateTimeOffset.UtcNow,
            ReleaseNotes = releaseNotes
        });
}
