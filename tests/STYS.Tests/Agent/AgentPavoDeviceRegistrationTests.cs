using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using STYS.Agent.Contracts.Dtos;
using STYS.Agent.Entities;
using STYS.Entegrasyonlar.Pos.Dtos;
using STYS.Entegrasyonlar.Pos.Entities;
using STYS.Entegrasyonlar.Pos.Mapping;
using STYS.Entegrasyonlar.Pos.Repositories;
using STYS.Entegrasyonlar.Pos.Services;
using STYS.Infrastructure.EntityFramework;
using STYS.Tests.TestSupport;
using STYS.Agent.Services;
using Xunit;
using AgentEntity = STYS.Agent.Entities.Agent;

namespace STYS.Tests.Agent;

[Collection(SqlServerIntegrationCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Domain", "Agent")]
[Trait("TestLevel", "SqlIntegration")]
public sealed class AgentPavoDeviceRegistrationTests : IAsyncLifetime
{
    private const string TestMarker = "pavoreg";
    private string _connectionString = string.Empty;
    private string _suffix = string.Empty;
    private string DeviceSerial => $"SER-{_suffix}";
    private string Terminal1Id => $"TERM-{_suffix}-1";
    private string Terminal2Id => $"TERM-{_suffix}-2";

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    [IntegrationFact]
    public async Task Register_UsesCurrentAgentContext_And_ResultDoesNotLeakFingerprint()
    {
        await using var db = await SetupAsync();
        if (db is null) return;

        var (agent, tesis) = await SeedAgentScopeAsync(db);
        var service = CreateService(db, agent.Id, agent.KurumId, tesis.Id);
        var request = BuildRequest(tesis.Id);

        var result = await service.RegisterFromAgentAsync(request, CancellationToken.None);

        Assert.Equal("Provisioned", result.ProvisioningStatus);
        Assert.True(result.CentralPosCihaziId > 0);

        var responseJson = System.Text.Json.JsonSerializer.Serialize(result);
        Assert.DoesNotContain("fingerprint", responseJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", responseJson, StringComparison.OrdinalIgnoreCase);

        var device = await db.PosCihazlari.SingleAsync(x => x.Id == result.CentralPosCihaziId);
        Assert.Equal(agent.Id, device.AgentId);
        Assert.Equal(agent.KurumId, device.KurumId);
        Assert.Equal(tesis.Id, device.TesisId);
        Assert.Equal($"local-{_suffix}", device.AgentLocalDeviceId);
        Assert.Equal($"SER-{_suffix}", device.SeriNo);
        Assert.Equal("Pavo POS", device.Ad);
        Assert.Equal(7, device.TransactionSequence);

        var terminal = await db.PosTerminaller.SingleAsync(x => x.PosCihaziId == device.Id && x.SerialNumber == Terminal1Id);
        Assert.Null(terminal.KasaBankaHesapId);
        Assert.True(terminal.AktifMi);
    }

    [IntegrationFact]
    public async Task Register_InvalidTesis_Rejected()
    {
        await using var db = await SetupAsync();
        if (db is null) return;

        var (agent, tesis) = await SeedAgentScopeAsync(db);
        var service = CreateService(db, agent.Id, agent.KurumId, tesis.Id);

        var ex = await Assert.ThrowsAsync<TOD.Platform.SharedKernel.Exceptions.BaseException>(() =>
            service.RegisterFromAgentAsync(BuildRequest(999999), CancellationToken.None));

        Assert.Equal(403, ex.ErrorCode);
    }

    [IntegrationFact]
    public async Task Register_CrossTenantSerialConflict_Rejected()
    {
        await using var db = await SetupAsync();
        if (db is null) return;

        var (agent, tesis) = await SeedAgentScopeAsync(db);
        await SeedOtherKurumDeviceAsync(db, DeviceSerial);
        var service = CreateService(db, agent.Id, agent.KurumId, tesis.Id);

        var ex = await Assert.ThrowsAsync<TOD.Platform.SharedKernel.Exceptions.BaseException>(() =>
            service.RegisterFromAgentAsync(BuildRequest(tesis.Id), CancellationToken.None));

        Assert.Equal(409, ex.ErrorCode);
    }

    [IntegrationFact]
    public async Task Register_SameTenantOtherAgent_Rejected()
    {
        await using var db = await SetupAsync();
        if (db is null) return;

        var (agent, tesis) = await SeedAgentScopeAsync(db);
        var otherAgent = await AgentTestSupport.SeedAgentAsync(db, agent.KurumId, $"{_suffix}-other-agent");
        await AttachAgentToTesisAsync(db, otherAgent.Id, agent.KurumId, tesis.Id);
        db.Set<PosCihazi>().Add(new PosCihazi
        {
            KurumId = agent.KurumId,
            TesisId = tesis.Id,
            AgentId = otherAgent.Id,
            Saglayici = PosSaglayici.Pavo,
            Ad = "Other Agent POS",
            SeriNo = DeviceSerial,
            IpAdresi = "192.168.1.20",
            HttpPort = 4567,
            HttpsPort = 4568,
            CreatedBy = "test",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = CreateService(db, agent.Id, agent.KurumId, tesis.Id);
        var ex = await Assert.ThrowsAsync<TOD.Platform.SharedKernel.Exceptions.BaseException>(() =>
            service.RegisterFromAgentAsync(BuildRequest(tesis.Id), CancellationToken.None));

        Assert.Equal(409, ex.ErrorCode);
    }

    [IntegrationFact]
    public async Task Register_SameTenantOtherTesis_Rejected()
    {
        await using var db = await SetupAsync();
        if (db is null) return;

        var (agent, tesis) = await SeedAgentScopeAsync(db);
        var otherTesis = await SeedOtherTesisAsync(db, agent.KurumId);
        await AttachAgentToTesisAsync(db, agent.Id, agent.KurumId, otherTesis.Id);
        db.Set<PosCihazi>().Add(new PosCihazi
        {
            KurumId = agent.KurumId,
            TesisId = otherTesis.Id,
            AgentId = agent.Id,
            Saglayici = PosSaglayici.Pavo,
            Ad = "Other Tesis POS",
            SeriNo = DeviceSerial,
            IpAdresi = "192.168.1.21",
            HttpPort = 4567,
            HttpsPort = 4568,
            CreatedBy = "test",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = CreateService(db, agent.Id, agent.KurumId, tesis.Id);
        var ex = await Assert.ThrowsAsync<TOD.Platform.SharedKernel.Exceptions.BaseException>(() =>
            service.RegisterFromAgentAsync(BuildRequest(tesis.Id), CancellationToken.None));

        Assert.Equal(409, ex.ErrorCode);
    }

    [IntegrationFact]
    public async Task Register_IsIdempotent_And_ReconcilePreservesAccountMapping()
    {
        await using var db = await SetupAsync();
        if (db is null) return;

        var (agent, tesis) = await SeedAgentScopeAsync(db);
        var service = CreateService(db, agent.Id, agent.KurumId, tesis.Id);
        var request = BuildRequest(tesis.Id, terminals:
        [
            new PavoDeviceProvisioningCandidateTerminal { AcquirerId = "A1", AcquirerName = "Acq 1", TerminalId = Terminal1Id, MerchantId = "M-1", Active = true, LastDiscoveredAt = DateTimeOffset.UtcNow },
            new PavoDeviceProvisioningCandidateTerminal { AcquirerId = "A1", AcquirerName = "Acq 1", TerminalId = Terminal2Id, MerchantId = "M-2", Active = true, LastDiscoveredAt = DateTimeOffset.UtcNow }
        ]);

        var first = await service.RegisterFromAgentAsync(request, CancellationToken.None);
        var terminal = await db.PosTerminaller.SingleAsync(x => x.PosCihaziId == first.CentralPosCihaziId && x.SerialNumber == Terminal1Id);
        terminal.KasaBankaHesapId = 123;
        await db.SaveChangesAsync();

        var second = await service.RegisterFromAgentAsync(request, CancellationToken.None);
        Assert.Equal(first.CentralPosCihaziId, second.CentralPosCihaziId);

        var refreshed = await db.PosTerminaller.AsNoTracking().Where(x => x.PosCihaziId == first.CentralPosCihaziId).ToListAsync();
        Assert.Equal(2, refreshed.Count);
        Assert.Equal(123, refreshed.Single(x => x.SerialNumber == Terminal1Id).KasaBankaHesapId);
        Assert.Null(refreshed.Single(x => x.SerialNumber == Terminal2Id).KasaBankaHesapId);
    }

    [IntegrationFact]
    public async Task Register_ConcurrentCalls_CreateSingleDevice()
    {
        await using var db = await SetupAsync();
        if (db is null) return;

        var (agent, tesis) = await SeedAgentScopeAsync(db);
        var request = BuildRequest(tesis.Id);

        async Task<AgentPavoDeviceRegistrationResult> ExecuteAsync()
        {
            await using var localDb = AgentTestSupport.CreateDbContext(_connectionString);
            var service = CreateService(localDb, agent.Id, agent.KurumId, tesis.Id);
            return await service.RegisterFromAgentAsync(request, CancellationToken.None);
        }

        var task1 = ExecuteAsync();
        var task2 = ExecuteAsync();
        await Task.WhenAll(task1, task2);

        var devices = await db.PosCihazlari.AsNoTracking().Where(x => x.SeriNo == DeviceSerial && !x.IsDeleted).ToListAsync();
        Assert.Single(devices);
        Assert.Equal(task1.Result.CentralPosCihaziId, task2.Result.CentralPosCihaziId);
    }

    [Fact]
    public void RegisterRequest_DoesNotExposeAgentOrTenantFields()
    {
        Assert.Null(typeof(AgentPavoDeviceRegisterRequest).GetProperty("AgentId"));
        Assert.Null(typeof(AgentPavoDeviceRegisterRequest).GetProperty("KurumId"));
    }

    private async Task<StysAppDbContext> SetupAsync()
    {
        _connectionString = Environment.GetEnvironmentVariable(IntegrationFactAttribute.ConnectionStringEnvVar);
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return null!;
        }

        _suffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];
        return AgentTestSupport.CreateDbContext(_connectionString);
    }

    private PosCihaziService CreateService(StysAppDbContext db, int agentId, int kurumId, int tesisId)
    {
        var mapper = CreateMapper();
        return new PosCihaziService(
            new PosCihaziRepository(db, mapper),
            mapper,
            new FakeSuperAdminTenantAccessor(),
            new FakeCurrentAgentContext
            {
                AgentId = agentId,
                KurumId = kurumId,
                TesisIds = [tesisId],
                IsAuthenticated = true
            },
            db,
            CreateAgentCommandService());
    }

    private AgentCommandService CreateAgentCommandService() =>
        new(new DbContextFactoryForTest<StysAppDbContext>(() => AgentTestSupport.CreateDbContext(_connectionString)), new FakeSuperAdminTenantAccessor(), NullLogger<AgentCommandService>.Instance);

    private static IMapper CreateMapper()
    {
        var config = new MapperConfiguration(cfg => cfg.AddMaps(typeof(PosCihaziProfile).Assembly), NullLoggerFactory.Instance);
        return config.CreateMapper();
    }

    private async Task<(AgentEntity agent, STYS.Tesisler.Entities.Tesis tesis)> SeedAgentScopeAsync(StysAppDbContext db)
    {
        var (kurum, _, tesis) = await AgentTestSupport.SeedKurumIlTesisAsync(db, _suffix);
        var agent = await AgentTestSupport.SeedAgentAsync(db, kurum.Id, _suffix);
        await AttachAgentToTesisAsync(db, agent.Id, kurum.Id, tesis.Id);
        return (agent, tesis);
    }

    private async Task<STYS.Tesisler.Entities.Tesis> SeedOtherTesisAsync(StysAppDbContext db, int kurumId)
    {
        var il = await db.Set<STYS.Iller.Entities.Il>().FirstAsync(x => x.Ad == $"Il-{_suffix}");
        var tesis = new STYS.Tesisler.Entities.Tesis
        {
            Ad = $"Other-{_suffix}",
            KurumId = kurumId,
            IlId = il.Id,
            Telefon = "000",
            Adres = "Adres",
            AktifMi = true
        };
        db.Set<STYS.Tesisler.Entities.Tesis>().Add(tesis);
        await db.SaveChangesAsync();
        return tesis;
    }

    private async Task<PosCihazi> SeedOtherKurumDeviceAsync(StysAppDbContext db, string serialNumber)
    {
        var (kurum, _, tesis) = await AgentTestSupport.SeedKurumIlTesisAsync(db, $"{_suffix}-other");
        var agent = await AgentTestSupport.SeedAgentAsync(db, kurum.Id, $"{_suffix}-other-agent");
        await AttachAgentToTesisAsync(db, agent.Id, kurum.Id, tesis.Id);

        var cihaz = new PosCihazi
        {
            KurumId = kurum.Id,
            TesisId = tesis.Id,
            AgentId = agent.Id,
            Saglayici = PosSaglayici.Pavo,
            Ad = "Other Kurum POS",
            SeriNo = serialNumber,
            IpAdresi = "192.168.1.20",
            HttpPort = 4567,
            HttpsPort = 4568,
            CreatedBy = "test",
            CreatedAt = DateTime.UtcNow
        };
        db.Set<PosCihazi>().Add(cihaz);
        await db.SaveChangesAsync();
        return cihaz;
    }

    private static async Task AttachAgentToTesisAsync(StysAppDbContext db, int agentId, int kurumId, int tesisId)
    {
        db.Set<AgentTesis>().Add(new AgentTesis
        {
            AgentId = agentId,
            KurumId = kurumId,
            TesisId = tesisId,
            AktifMi = true,
            CreatedBy = "test",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private AgentPavoDeviceRegisterRequest BuildRequest(int tesisId, IReadOnlyCollection<PavoDeviceProvisioningCandidateTerminal>? terminals = null) =>
        new()
        {
            LocalDeviceId = $"local-{_suffix}",
            Provider = "PAVO",
            DisplayName = "Pavo POS",
            Host = "192.168.1.50",
            HttpPort = 4567,
            HttpsPort = 4568,
            Protocol = "HTTP",
            SerialNumber = DeviceSerial,
            DeviceName = "Pavo X",
            PairedAt = DateTimeOffset.UtcNow,
            TesisId = tesisId,
            Fingerprint = "FP-001",
            TargetFingerprint = "TFP-001",
            TransactionSequence = 7,
            Terminals = terminals ?? new[]
            {
                new PavoDeviceProvisioningCandidateTerminal
                {
                    AcquirerId = "A1",
                    AcquirerName = "Acq 1",
                    TerminalId = Terminal1Id,
                    MerchantId = "M-1",
                    Active = true,
                    LastDiscoveredAt = DateTimeOffset.UtcNow
                }
            }
        };
}
