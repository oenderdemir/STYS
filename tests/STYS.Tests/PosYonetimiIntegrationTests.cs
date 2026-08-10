using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using STYS.Agent.Contracts.Dtos;
using STYS.Agent.Contracts.Enums;
using STYS.Agent.Entities;
using STYS.Agent.Services;
using AgentEntity = STYS.Agent.Entities.Agent;
using STYS.Entegrasyonlar.Pos.Dtos;
using STYS.Entegrasyonlar.Pos.Entities;
using STYS.Entegrasyonlar.Pos.Mapping;
using STYS.Entegrasyonlar.Pos.Repositories;
using STYS.Entegrasyonlar.Pos.Services;
using STYS.Infrastructure.EntityFramework;
using STYS.Iller.Entities;
using STYS.Kurumlar.Entities;
using STYS.Muhasebe.KasaBankaHesaplari.Entities;
using STYS.Tests.Agent;
using STYS.Tests.TestSupport;
using STYS.Tesisler.Entities;
using TOD.Platform.SharedKernel.Exceptions;
using Xunit;

namespace STYS.Tests;

[Collection(SqlServerIntegrationCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Domain", "Pos")]
[Trait("TestLevel", "SqlIntegration")]
[Trait("Dependency", "SqlServer")]
public sealed class PosYonetimiIntegrationTests
{
    private const string TestMarker = "POSY";

    [IntegrationFact]
    public async Task UpdateAsync_CrossKurumTesisAtanamaz()
    {
        var cs = ConnectionString();
        if (cs is null) return;

        var suffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];
        await using var seedDb = AgentTestSupport.CreateDbContext(cs);
        await seedDb.Database.MigrateAsync();
        var fixture = await SeedAsync(seedDb, suffix);

        await using var repoDb = AgentTestSupport.CreateDbContext(cs);
        var service = CreateCihazService(repoDb, cs, fixture.KurumId);

        var dto = new PosCihaziDto
        {
            Id = fixture.DeviceId,
            KurumId = fixture.KurumId,
            TesisId = fixture.OtherKurumTesisId,
            AgentId = fixture.MainAgentId,
            Saglayici = (int)PosSaglayici.Pavo,
            Ad = $"Updated-{suffix}",
            SeriNo = fixture.DeviceSerial,
            AktifMi = true
        };

        await Assert.ThrowsAsync<BaseException>(() => service.UpdateAsync(dto));

        await CleanupAsync(seedDb, suffix);
    }

    [IntegrationFact]
    public async Task UpdateAsync_DuplicateSeriNoEngellenir_AndSaglayiciGuncellenebilir()
    {
        var cs = ConnectionString();
        if (cs is null) return;

        var suffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];
        await using var seedDb = AgentTestSupport.CreateDbContext(cs);
        await seedDb.Database.MigrateAsync();
        var fixture = await SeedAsync(seedDb, suffix);

        await using var repoDb = AgentTestSupport.CreateDbContext(cs);
        var service = CreateCihazService(repoDb, cs, fixture.KurumId);

        var updated = await service.UpdateAsync(new PosCihaziDto
        {
            Id = fixture.DeviceId,
            KurumId = fixture.KurumId,
            TesisId = fixture.MainTesisId,
            AgentId = fixture.MainAgentId,
            Saglayici = (int)PosSaglayici.Diger,
            Ad = $"Updated-{suffix}",
            SeriNo = fixture.DeviceSerial,
            AktifMi = true
        });

        Assert.Equal((int)PosSaglayici.Diger, updated.Saglayici);
        Assert.Equal(fixture.MainTesisName, updated.TesisAd);
        Assert.Equal(fixture.MainAgentName, updated.AgentAd);

        await Assert.ThrowsAsync<BaseException>(() => service.UpdateAsync(new PosCihaziDto
        {
            Id = fixture.SecondDeviceId,
            KurumId = fixture.KurumId,
            TesisId = fixture.MainTesisId,
            AgentId = fixture.MainAgentId,
            Saglayici = (int)PosSaglayici.Pavo,
            Ad = $"Second-{suffix}",
            SeriNo = fixture.DeviceSerial,
            AktifMi = true
        }));

        await CleanupAsync(seedDb, suffix);
    }

    [IntegrationFact]
    public async Task UpdateAsync_BaskaTesiseAitAgentAtanamaz()
    {
        var cs = ConnectionString();
        if (cs is null) return;

        var suffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];
        await using var seedDb = AgentTestSupport.CreateDbContext(cs);
        await seedDb.Database.MigrateAsync();
        var fixture = await SeedAsync(seedDb, suffix);

        await using var repoDb = AgentTestSupport.CreateDbContext(cs);
        var service = CreateCihazService(repoDb, cs, fixture.KurumId);

        var dto = new PosCihaziDto
        {
            Id = fixture.DeviceId,
            KurumId = fixture.KurumId,
            TesisId = fixture.MainTesisId,
            AgentId = fixture.OtherTesisAgentId,
            Saglayici = (int)PosSaglayici.Pavo,
            Ad = $"Updated-{suffix}",
            SeriNo = fixture.DeviceSerial,
            AktifMi = true
        };

        await Assert.ThrowsAsync<BaseException>(() => service.UpdateAsync(dto));

        await CleanupAsync(seedDb, suffix);
    }

    [IntegrationFact]
    public async Task TerminalKaydetAsync_CrossKurumHesabaBaglanamaz()
    {
        var cs = ConnectionString();
        if (cs is null) return;

        var suffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];
        await using var db = AgentTestSupport.CreateDbContext(cs);
        await db.Database.MigrateAsync();
        var fixture = await SeedAsync(db, suffix);
        var service = CreateTerminalService(db, fixture.KurumId);

        var request = BuildTerminalRequest(fixture, fixture.OtherKurumHesapId, suffix, "TERM-1");
        await Assert.ThrowsAsync<BaseException>(() => service.KaydetAsync(fixture.DeviceId, null, request, CancellationToken.None));

        await CleanupAsync(db, suffix);
    }

    [IntegrationFact]
    public async Task TerminalKaydetAsync_CrossTesisHesabaBaglanamaz()
    {
        var cs = ConnectionString();
        if (cs is null) return;

        var suffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];
        await using var db = AgentTestSupport.CreateDbContext(cs);
        await db.Database.MigrateAsync();
        var fixture = await SeedAsync(db, suffix);
        var service = CreateTerminalService(db, fixture.KurumId);

        var request = BuildTerminalRequest(fixture, fixture.OtherTesisHesapId, suffix, "TERM-2");
        await Assert.ThrowsAsync<BaseException>(() => service.KaydetAsync(fixture.DeviceId, null, request, CancellationToken.None));

        await CleanupAsync(db, suffix);
    }

    [IntegrationFact]
    public async Task TerminalKaydetAsync_DuplicateDeviceTerminalIdEngellenir()
    {
        var cs = ConnectionString();
        if (cs is null) return;

        var suffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];
        await using var db = AgentTestSupport.CreateDbContext(cs);
        await db.Database.MigrateAsync();
        var fixture = await SeedAsync(db, suffix);
        var service = CreateTerminalService(db, fixture.KurumId);

        var first = await service.KaydetAsync(fixture.DeviceId, null, BuildTerminalRequest(fixture, fixture.MainKrediHesapId, suffix, "TERM-3"), CancellationToken.None);
        Assert.Equal("TERM-3", first.TerminalId);

        await Assert.ThrowsAsync<BaseException>(() => service.KaydetAsync(fixture.DeviceId, null, BuildTerminalRequest(fixture, fixture.MainKrediHesapId, suffix, "TERM-3"), CancellationToken.None));

        await CleanupAsync(db, suffix);
    }

    [IntegrationFact]
    public async Task TerminalKaydetAsync_AyniCihazdaIkiFarkliTerminalOlusturulabilir()
    {
        var cs = ConnectionString();
        if (cs is null) return;

        var suffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];
        await using var db = AgentTestSupport.CreateDbContext(cs);
        await db.Database.MigrateAsync();
        var fixture = await SeedAsync(db, suffix);
        var service = CreateTerminalService(db, fixture.KurumId);

        await service.KaydetAsync(fixture.DeviceId, null, BuildTerminalRequest(fixture, fixture.MainKrediHesapId, suffix, "TERM-4A"), CancellationToken.None);
        await service.KaydetAsync(fixture.DeviceId, null, BuildTerminalRequest(fixture, fixture.MainKrediHesapId, suffix, "TERM-4B"), CancellationToken.None);

        var count = await db.PosTerminaller.IgnoreQueryFilters().CountAsync(x => x.PosCihaziId == fixture.DeviceId && !x.IsDeleted);
        Assert.Equal(2, count);

        await CleanupAsync(db, suffix);
    }

    [IntegrationFact]
    public async Task TerminalKaydetAsync_HesapsizOlusturulabilir()
    {
        var cs = ConnectionString();
        if (cs is null) return;

        var suffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];
        await using var db = AgentTestSupport.CreateDbContext(cs);
        await db.Database.MigrateAsync();
        var fixture = await SeedAsync(db, suffix);
        var service = CreateTerminalService(db, fixture.KurumId);

        var saved = await service.KaydetAsync(fixture.DeviceId, null, BuildTerminalRequest(fixture, null, suffix, "TERM-NO-ACC"), CancellationToken.None);

        Assert.Null(saved.KasaBankaHesapId);
        Assert.Equal("Hesap eşleştirilmedi", saved.KasaBankaHesapAd);

        await CleanupAsync(db, suffix);
    }

    [IntegrationFact]
    public async Task DeleteAsync_CihazSoftDeleteYaparVeTerminalleriKoru()
    {
        var cs = ConnectionString();
        if (cs is null) return;

        var suffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];
        await using var db = AgentTestSupport.CreateDbContext(cs);
        await db.Database.MigrateAsync();
        var fixture = await SeedAsync(db, suffix);
        var terminalService = CreateTerminalService(db, fixture.KurumId);
        await terminalService.KaydetAsync(fixture.DeviceId, null, BuildTerminalRequest(fixture, fixture.MainKrediHesapId, suffix, "TERM-5"), CancellationToken.None);

        await using (var repoDb = AgentTestSupport.CreateDbContext(cs))
        {
            var cihazService = CreateCihazService(repoDb, cs, fixture.KurumId);

            await cihazService.DeleteAsync(fixture.DeviceId);
        }

        await using var verifyDb = AgentTestSupport.CreateDbContext(cs);
        var cihaz = await verifyDb.PosCihazlari.IgnoreQueryFilters().SingleAsync(x => x.Id == fixture.DeviceId);
        var terminal = await verifyDb.PosTerminaller.IgnoreQueryFilters().SingleAsync(x => x.PosCihaziId == fixture.DeviceId);
        Assert.True(cihaz.IsDeleted);
        Assert.False(cihaz.AktifMi);
        Assert.True(terminal.IsDeleted);
        Assert.False(terminal.AktifMi);

        await CleanupAsync(db, suffix);
    }

    [IntegrationFact]
    public async Task PavoPairing_CommandUretir_SequenceArttirir_vePayloadCihazBazlidir()
    {
        var cs = ConnectionString();
        if (cs is null) return;

        var suffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];
        await using var db = AgentTestSupport.CreateDbContext(cs);
        await db.Database.MigrateAsync();
        var fixture = await SeedAsync(db, suffix);

        var device = await db.PosCihazlari.FirstAsync(x => x.Id == fixture.DeviceId);
        device.IpAdresi = "127.0.0.1";
        device.HttpPort = 4567;
        device.HttpsPort = null;
        device.Fingerprint = "FP-DEVICE";
        await db.SaveChangesAsync();

        var service = CreateCihazService(db, cs, fixture.KurumId);
        var first = await service.PairingAsync(fixture.DeviceId, "test", CancellationToken.None);
        var second = await service.PingAsync(fixture.DeviceId, "test", CancellationToken.None);

        Assert.Equal("PavoPairing", first.CommandType);
        Assert.Equal("PavoPing", second.CommandType);

        await using var verifyDb = AgentTestSupport.CreateDbContext(cs);
        var refreshed = await verifyDb.PosCihazlari.AsNoTracking().SingleAsync(x => x.Id == fixture.DeviceId);
        Assert.Equal(2, refreshed.TransactionSequence);

        var firstPayload = JsonSerializer.Deserialize<PavoPairingRequest>(first.Payload ?? string.Empty, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var secondPayload = JsonSerializer.Deserialize<PavoPingRequest>(second.Payload ?? string.Empty, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(firstPayload);
        Assert.NotNull(secondPayload);
        Assert.Equal(fixture.DeviceId, firstPayload!.PosCihaziId);
        Assert.Equal(0, firstPayload.TransactionHandle.TransactionSequence);
        Assert.Equal("127.0.0.1", firstPayload.IpAddress);
        Assert.Equal(fixture.DeviceId, secondPayload!.PosCihaziId);
        Assert.Equal(1, secondPayload.TransactionHandle.TransactionSequence);

        await CleanupAsync(db, suffix);
    }

    [IntegrationFact]
    public async Task PavoSequence_ParallelUnique_veRestartSonrasiDevamEder()
    {
        var cs = ConnectionString();
        if (cs is null) return;

        var suffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];
        await using var seedDb = AgentTestSupport.CreateDbContext(cs);
        await seedDb.Database.MigrateAsync();
        var fixture = await SeedAsync(seedDb, suffix);

        var device = await seedDb.PosCihazlari.FirstAsync(x => x.Id == fixture.DeviceId);
        device.IpAdresi = "127.0.0.1";
        device.HttpPort = 4567;
        device.Fingerprint = "FP-DEVICE";
        await seedDb.SaveChangesAsync();

        var pairingService = CreateCihazService(seedDb, cs, fixture.KurumId);
        await pairingService.PairingAsync(fixture.DeviceId, "test", CancellationToken.None);

        await using var db1 = AgentTestSupport.CreateDbContext(cs);
        await using var db2 = AgentTestSupport.CreateDbContext(cs);
        var service1 = CreateCihazService(db1, cs, fixture.KurumId);
        var service2 = CreateCihazService(db2, cs, fixture.KurumId);

        var pingTask = service1.PingAsync(fixture.DeviceId, "test", CancellationToken.None);
        var infoTask = service2.GetDeviceInfoAsync(fixture.DeviceId, "test", CancellationToken.None);
        await Task.WhenAll(pingTask, infoTask);

        var pingPayload = JsonSerializer.Deserialize<PavoPingRequest>(pingTask.Result.Payload ?? string.Empty, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var infoPayload = JsonSerializer.Deserialize<PavoGetDeviceInfoRequest>(infoTask.Result.Payload ?? string.Empty, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(pingPayload);
        Assert.NotNull(infoPayload);
        Assert.Equal(1, pingPayload!.TransactionHandle.TransactionSequence);
        Assert.Equal(2, infoPayload!.TransactionHandle.TransactionSequence);

        await using (var verifyDb = AgentTestSupport.CreateDbContext(cs))
        {
            var refreshed = await verifyDb.PosCihazlari.AsNoTracking().SingleAsync(x => x.Id == fixture.DeviceId);
            Assert.Equal(2, refreshed.TransactionSequence);
        }

        await using var db3 = AgentTestSupport.CreateDbContext(cs);
        var restartService = CreateCihazService(db3, cs, fixture.KurumId);
        var restartPing = await restartService.PingAsync(fixture.DeviceId, "test", CancellationToken.None);
        var restartPayload = JsonSerializer.Deserialize<PavoPingRequest>(restartPing.Payload ?? string.Empty, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(restartPayload);
        Assert.Equal(3, restartPayload!.TransactionHandle.TransactionSequence);

        await CleanupAsync(seedDb, suffix);
    }

    [IntegrationFact]
    public async Task PavoGetDeviceInfoCompletion_TerminalDiscoveryYaparVeHesabiKorumayaDevamEder()
    {
        var cs = ConnectionString();
        if (cs is null) return;

        var suffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];
        await using var db = AgentTestSupport.CreateDbContext(cs);
        await db.Database.MigrateAsync();
        var fixture = await SeedAsync(db, suffix);

        var device = await db.PosCihazlari.FirstAsync(x => x.Id == fixture.DeviceId);
        device.IpAdresi = "127.0.0.1";
        device.HttpPort = 4567;
        device.Fingerprint = "FP-DEVICE";
        await db.SaveChangesAsync();

        var terminal = new PosTerminal
        {
            KurumId = fixture.KurumId,
            TesisId = fixture.MainTesisId,
            PosCihaziId = fixture.DeviceId,
            KasaBankaHesapId = fixture.MainKrediHesapId,
            SaglayiciKodu = "PAVO",
            AcquirerId = "OLD-ACQ",
            AcquirerName = "Old Acquirer",
            Ad = "Existing Terminal",
            SerialNumber = "TERM-OLD",
            SourceTerminalReference = "MER-OLD",
            SourceFingerprint = "FP-OLD",
            AktifMi = true,
            CreatedBy = "test",
            CreatedAt = DateTime.UtcNow
        };
        db.PosTerminaller.Add(terminal);
        await db.SaveChangesAsync();

        var deviceService = CreateCihazService(db, cs, fixture.KurumId);
        var agentService = CreateAgentCommandService(cs, fixture.KurumId);

        var command = await deviceService.GetDeviceInfoAsync(fixture.DeviceId, "test", CancellationToken.None);
        await agentService.AcceptAsync(command.Id, fixture.MainAgentId, CancellationToken.None);
        await agentService.SetRunningAsync(command.Id, fixture.MainAgentId, CancellationToken.None);

        var response = new PavoGetDeviceInfoResponse
        {
            Fingerprint = "FP-NEW",
            TargetFingerprint = "TFP-NEW",
            Terminals =
            [
                new PavoDeviceTerminalInfo
                {
                    TerminalId = "TERM-NEW",
                    MerchantId = "MER-NEW",
                    AcquirerId = "ACQ-NEW",
                    AcquirerName = "New Acquirer"
                }
            ]
        };

        await agentService.CompleteAsync(command.Id, fixture.MainAgentId, new AgentCommandCompleteRequest
        {
            Id = command.Id,
            Success = true,
            ResultPayload = JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web))
        }, CancellationToken.None);

        await using var verifyDb = AgentTestSupport.CreateDbContext(cs);
        var updatedDevice = await verifyDb.PosCihazlari.AsNoTracking().SingleAsync(x => x.Id == fixture.DeviceId);
        Assert.Equal("FP-NEW", updatedDevice.Fingerprint);
        Assert.Equal("TFP-NEW", updatedDevice.TargetFingerprint);
        Assert.NotNull(updatedDevice.SonBaglantiTarihi);

        var terminals = await verifyDb.PosTerminaller.AsNoTracking()
            .Where(x => x.PosCihaziId == fixture.DeviceId && !x.IsDeleted)
            .OrderBy(x => x.SerialNumber)
            .ToListAsync();

        Assert.Single(terminals);
        var discovered = terminals.Single(x => x.SerialNumber == "TERM-NEW");

        var missing = await verifyDb.PosTerminaller.AsNoTracking().SingleAsync(x => x.SerialNumber == "TERM-OLD");
        Assert.False(missing.IsDeleted);
        Assert.False(missing.AktifMi);
        Assert.Equal(fixture.MainKrediHesapId, missing.KasaBankaHesapId);
        Assert.Equal("OLD-ACQ", missing.AcquirerId);
        Assert.Equal("Old Acquirer", missing.AcquirerName);

        Assert.Null(discovered.KasaBankaHesapId);
        Assert.Equal("ACQ-NEW", discovered.AcquirerId);
        Assert.Equal("New Acquirer", discovered.AcquirerName);

        await CleanupAsync(db, suffix);
    }

    [IntegrationFact]
    public async Task PavoResult_CrossAgentEngellenir()
    {
        var cs = ConnectionString();
        if (cs is null) return;

        var suffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];
        await using var db = AgentTestSupport.CreateDbContext(cs);
        await db.Database.MigrateAsync();
        var fixture = await SeedAsync(db, suffix);

        var device = await db.PosCihazlari.FirstAsync(x => x.Id == fixture.DeviceId);
        device.IpAdresi = "127.0.0.1";
        device.HttpPort = 4567;
        await db.SaveChangesAsync();

        var deviceService = CreateCihazService(db, cs, fixture.KurumId);
        var agentService = CreateAgentCommandService(cs, fixture.KurumId);
        var command = await deviceService.GetDeviceInfoAsync(fixture.DeviceId, "test", CancellationToken.None);
        await agentService.AcceptAsync(command.Id, fixture.MainAgentId, CancellationToken.None);
        await agentService.SetRunningAsync(command.Id, fixture.MainAgentId, CancellationToken.None);

        var deviceRow = await db.PosCihazlari.FirstAsync(x => x.Id == fixture.DeviceId);
        deviceRow.AgentId = fixture.OtherTesisAgentId;
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<BaseException>(() => agentService.CompleteAsync(command.Id, fixture.MainAgentId, new AgentCommandCompleteRequest
        {
            Id = command.Id,
            Success = true,
            ResultPayload = JsonSerializer.Serialize(new PavoGetDeviceInfoResponse(), new JsonSerializerOptions(JsonSerializerDefaults.Web))
        }, CancellationToken.None));

        await CleanupAsync(db, suffix);
    }

    [IntegrationFact]
    public async Task PavoResult_CrossKurumEngellenir()
    {
        var cs = ConnectionString();
        if (cs is null) return;

        var suffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];
        await using var db = AgentTestSupport.CreateDbContext(cs);
        await db.Database.MigrateAsync();
        var fixture = await SeedAsync(db, suffix);

        var device = await db.PosCihazlari.FirstAsync(x => x.Id == fixture.DeviceId);
        device.IpAdresi = "127.0.0.1";
        device.HttpPort = 4567;
        await db.SaveChangesAsync();

        var deviceService = CreateCihazService(db, cs, fixture.KurumId);
        var agentService = CreateAgentCommandService(cs, fixture.KurumId);
        var command = await deviceService.GetDeviceInfoAsync(fixture.DeviceId, "test", CancellationToken.None);
        await agentService.AcceptAsync(command.Id, fixture.MainAgentId, CancellationToken.None);
        await agentService.SetRunningAsync(command.Id, fixture.MainAgentId, CancellationToken.None);

        var deviceRow = await db.PosCihazlari.FirstAsync(x => x.Id == fixture.DeviceId);
        deviceRow.KurumId = fixture.KurumId + 999999;
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<BaseException>(() => agentService.CompleteAsync(command.Id, fixture.MainAgentId, new AgentCommandCompleteRequest
        {
            Id = command.Id,
            Success = true,
            ResultPayload = JsonSerializer.Serialize(new PavoGetDeviceInfoResponse(), new JsonSerializerOptions(JsonSerializerDefaults.Web))
        }, CancellationToken.None));

        await CleanupAsync(db, suffix);
    }

    private static string? ConnectionString() => Environment.GetEnvironmentVariable("STYS_INTEGRATION_TEST_CONNECTION_STRING");

    private static IMapper CreateMapper()
    {
        var config = new MapperConfiguration(cfg => cfg.AddMaps(typeof(PosCihaziProfile).Assembly), NullLoggerFactory.Instance);
        return config.CreateMapper();
    }

    private static AgentCommandService CreateAgentCommandService(string connectionString, int kurumId) =>
        new(new DbContextFactoryForTest<StysAppDbContext>(() => AgentTestSupport.CreateDbContext(connectionString)), new FakeKurumTenantAccessor(kurumId), NullLogger<AgentCommandService>.Instance);

    private static PosCihaziService CreateCihazService(StysAppDbContext db, string connectionString, int kurumId)
    {
        var mapper = CreateMapper();
        return new PosCihaziService(
            new PosCihaziRepository(db, mapper),
            mapper,
            new FakeKurumTenantAccessor(kurumId),
            db,
            CreateAgentCommandService(connectionString, kurumId));
    }

    private static PosTerminalService CreateTerminalService(StysAppDbContext db, int kurumId) =>
        new(db, [new FakePavoSaglayici()], new FakeKurumTenantAccessor(kurumId));

    private static PosTerminalKaydetRequest BuildTerminalRequest(Fixture fixture, int? hesapId, string suffix, string terminalId) =>
        new()
        {
            PosCihaziId = fixture.DeviceId,
            TesisId = fixture.MainTesisId,
            KasaBankaHesapId = hesapId,
            SaglayiciKodu = "PAVO",
            Ad = $"Terminal-{suffix}-{terminalId}",
            TerminalId = terminalId,
            MerchantId = $"M-{suffix}-{terminalId}",
            SerialNumber = terminalId,
            SourceFingerprint = $"FP-{suffix}",
            SourceTerminalReference = $"M-{suffix}-{terminalId}",
            AktifMi = true
        };

    private static async Task<Fixture> SeedAsync(StysAppDbContext db, string suffix)
    {
        var il = new Il { Ad = $"Il-{suffix}", AktifMi = true };
        db.Set<Il>().Add(il);
        await db.SaveChangesAsync();

        var kurum = new Kurum { Kod = $"KRM-{suffix}", Ad = $"Kurum-{suffix}", AktifMi = true };
        db.Set<Kurum>().Add(kurum);
        await db.SaveChangesAsync();

        var mainTesis = new Tesis { Ad = $"Tesis-{suffix}-A", KurumId = kurum.Id, IlId = il.Id, Telefon = "000", Adres = "Adres", AktifMi = true };
        var otherTesis = new Tesis { Ad = $"Tesis-{suffix}-B", KurumId = kurum.Id, IlId = il.Id, Telefon = "111", Adres = "Adres2", AktifMi = true };
        db.Set<Tesis>().AddRange(mainTesis, otherTesis);
        await db.SaveChangesAsync();

        var otherKurum = new Kurum { Kod = $"KRM2-{suffix}", Ad = $"Kurum2-{suffix}", AktifMi = true };
        db.Set<Kurum>().Add(otherKurum);
        await db.SaveChangesAsync();

        var otherKurumTesis = new Tesis { Ad = $"Tesis-{suffix}-C", KurumId = otherKurum.Id, IlId = il.Id, Telefon = "222", Adres = "Adres3", AktifMi = true };
        db.Set<Tesis>().Add(otherKurumTesis);
        await db.SaveChangesAsync();

        var mainAgent = new AgentEntity
        {
            Ad = $"Agent-{suffix}-A",
            AgentKey = $"AG-{Guid.NewGuid():N}"[..16],
            KurumId = kurum.Id,
            Durum = STYS.Agent.Contracts.Enums.AgentDurum.Active,
            CreatedBy = "test",
            CreatedAt = DateTime.UtcNow
        };
        var otherTesisAgent = new AgentEntity
        {
            Ad = $"Agent-{suffix}-B",
            AgentKey = $"AG-{Guid.NewGuid():N}"[..16],
            KurumId = kurum.Id,
            Durum = STYS.Agent.Contracts.Enums.AgentDurum.Active,
            CreatedBy = "test",
            CreatedAt = DateTime.UtcNow
        };
        var otherKurumAgent = new AgentEntity
        {
            Ad = $"Agent-{suffix}-C",
            AgentKey = $"AG-{Guid.NewGuid():N}"[..16],
            KurumId = otherKurum.Id,
            Durum = STYS.Agent.Contracts.Enums.AgentDurum.Active,
            CreatedBy = "test",
            CreatedAt = DateTime.UtcNow
        };
        db.Set<AgentEntity>().AddRange(mainAgent, otherTesisAgent, otherKurumAgent);
        await db.SaveChangesAsync();
        db.Set<AgentScope>().AddRange(
            new AgentScope { AgentId = mainAgent.Id, Scope = "agent.command.execute", AktifMi = true, CreatedBy = "test", CreatedAt = DateTime.UtcNow },
            new AgentScope { AgentId = mainAgent.Id, Scope = "agent.command.read", AktifMi = true, CreatedBy = "test", CreatedAt = DateTime.UtcNow },
            new AgentScope { AgentId = mainAgent.Id, Scope = "agent.result.write", AktifMi = true, CreatedBy = "test", CreatedAt = DateTime.UtcNow },
            new AgentScope { AgentId = mainAgent.Id, Scope = "agent.heartbeat", AktifMi = true, CreatedBy = "test", CreatedAt = DateTime.UtcNow });
        db.Set<AgentCapability>().Add(new AgentCapability { AgentId = mainAgent.Id, Capability = "pavo", AktifMi = true, CreatedBy = "test", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        db.Set<AgentTesis>().AddRange(
            new AgentTesis { AgentId = mainAgent.Id, KurumId = kurum.Id, TesisId = mainTesis.Id, AktifMi = true, CreatedBy = "test", CreatedAt = DateTime.UtcNow },
            new AgentTesis { AgentId = otherTesisAgent.Id, KurumId = kurum.Id, TesisId = otherTesis.Id, AktifMi = true, CreatedBy = "test", CreatedAt = DateTime.UtcNow },
            new AgentTesis { AgentId = otherKurumAgent.Id, KurumId = otherKurum.Id, TesisId = otherKurumTesis.Id, AktifMi = true, CreatedBy = "test", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var mainHesap = new KasaBankaHesap { TesisId = mainTesis.Id, Tip = KasaBankaHesapTipleri.KrediKarti, Kod = $"KK-{suffix}-A", Ad = $"Kredi Kartı {suffix} A", ParaBirimi = "TRY", AktifMi = true };
        var otherTesisHesap = new KasaBankaHesap { TesisId = otherTesis.Id, Tip = KasaBankaHesapTipleri.KrediKarti, Kod = $"KK-{suffix}-B", Ad = $"Kredi Kartı {suffix} B", ParaBirimi = "TRY", AktifMi = true };
        var otherKurumHesap = new KasaBankaHesap { TesisId = otherKurumTesis.Id, Tip = KasaBankaHesapTipleri.KrediKarti, Kod = $"KK-{suffix}-C", Ad = $"Kredi Kartı {suffix} C", ParaBirimi = "TRY", AktifMi = true };
        db.Set<KasaBankaHesap>().AddRange(mainHesap, otherTesisHesap, otherKurumHesap);
        await db.SaveChangesAsync();

        var device = new PosCihazi
        {
            KurumId = kurum.Id,
            TesisId = mainTesis.Id,
            AgentId = mainAgent.Id,
            Saglayici = PosSaglayici.Pavo,
            Ad = $"POS-{suffix}",
            SeriNo = $"SER-{suffix}-1",
            AktifMi = true,
            CreatedBy = "test",
            CreatedAt = DateTime.UtcNow
        };
        var secondDevice = new PosCihazi
        {
            KurumId = kurum.Id,
            TesisId = mainTesis.Id,
            AgentId = mainAgent.Id,
            Saglayici = PosSaglayici.Pavo,
            Ad = $"POS-{suffix}-2",
            SeriNo = $"SER-{suffix}-2",
            AktifMi = true,
            CreatedBy = "test",
            CreatedAt = DateTime.UtcNow
        };
        db.Set<PosCihazi>().AddRange(device, secondDevice);
        await db.SaveChangesAsync();

        return new Fixture(
            kurum.Id,
            mainTesis.Id,
            mainTesis.Ad,
            otherTesis.Id,
            otherKurumTesis.Id,
            mainAgent.Id,
            mainAgent.Ad,
            otherTesisAgent.Id,
            otherKurumAgent.Id,
            mainHesap.Id,
            otherTesisHesap.Id,
            otherKurumHesap.Id,
            device.Id,
            secondDevice.Id,
            device.SeriNo);
    }

    private static async Task CleanupAsync(StysAppDbContext db, string suffix)
    {
        var deviceIds = await db.Set<PosCihazi>().IgnoreQueryFilters()
            .Where(x => x.Ad.Contains(suffix) || x.SeriNo.Contains(suffix))
            .Select(x => x.Id)
            .ToListAsync();
        if (deviceIds.Count > 0)
        {
            await db.Set<PosTerminal>().IgnoreQueryFilters()
                .Where(x => x.PosCihaziId.HasValue && deviceIds.Contains(x.PosCihaziId.Value))
                .ExecuteDeleteAsync();
            await db.Set<PosCihazi>().IgnoreQueryFilters().Where(x => deviceIds.Contains(x.Id)).ExecuteDeleteAsync();
        }

        var hesapIds = await db.Set<KasaBankaHesap>().IgnoreQueryFilters()
            .Where(x => x.Kod.Contains(suffix) || x.Ad.Contains(suffix))
            .Select(x => x.Id)
            .ToListAsync();
        if (hesapIds.Count > 0)
        {
            await db.Set<KasaBankaHesap>().IgnoreQueryFilters().Where(x => hesapIds.Contains(x.Id)).ExecuteDeleteAsync();
        }

        var agentIds = await db.Set<AgentEntity>().IgnoreQueryFilters()
            .Where(x => x.Ad.Contains(suffix) || x.AgentKey.Contains(suffix))
            .Select(x => x.Id)
            .ToListAsync();
        if (agentIds.Count > 0)
        {
            await db.Set<AgentTesis>().IgnoreQueryFilters().Where(x => agentIds.Contains(x.AgentId)).ExecuteDeleteAsync();
            await db.Set<AgentCredential>().IgnoreQueryFilters().Where(x => agentIds.Contains(x.AgentId)).ExecuteDeleteAsync();
            await db.Set<AgentCapability>().IgnoreQueryFilters().Where(x => agentIds.Contains(x.AgentId)).ExecuteDeleteAsync();
            await db.Set<AgentScope>().IgnoreQueryFilters().Where(x => agentIds.Contains(x.AgentId)).ExecuteDeleteAsync();
            await db.Set<AgentEnrollment>().IgnoreQueryFilters().Where(x => x.AgentId.HasValue && agentIds.Contains(x.AgentId.Value)).ExecuteDeleteAsync();
            await db.Set<AgentEntity>().IgnoreQueryFilters().Where(x => agentIds.Contains(x.Id)).ExecuteDeleteAsync();
        }

        var tesisIds = await db.Set<Tesis>().IgnoreQueryFilters()
            .Where(x => x.Ad.Contains(suffix))
            .Select(x => x.Id)
            .ToListAsync();
        if (tesisIds.Count > 0)
        {
            await db.Set<Tesis>().IgnoreQueryFilters().Where(x => tesisIds.Contains(x.Id)).ExecuteDeleteAsync();
        }

        var kurumIds = await db.Set<Kurum>().IgnoreQueryFilters()
            .Where(x => x.Ad.Contains(suffix) || x.Kod.Contains(suffix))
            .Select(x => x.Id)
            .ToListAsync();
        if (kurumIds.Count > 0)
        {
            await db.Set<Kurum>().IgnoreQueryFilters().Where(x => kurumIds.Contains(x.Id)).ExecuteDeleteAsync();
        }

        var ilIds = await db.Set<Il>().IgnoreQueryFilters()
            .Where(x => x.Ad.Contains(suffix))
            .Select(x => x.Id)
            .ToListAsync();
        if (ilIds.Count > 0)
        {
            await db.Set<Il>().IgnoreQueryFilters().Where(x => ilIds.Contains(x.Id)).ExecuteDeleteAsync();
        }
    }

    private sealed record Fixture(
        int KurumId,
        int MainTesisId,
        string MainTesisName,
        int OtherTesisId,
        int OtherKurumTesisId,
        int MainAgentId,
        string MainAgentName,
        int OtherTesisAgentId,
        int OtherKurumAgentId,
        int MainKrediHesapId,
        int OtherTesisHesapId,
        int OtherKurumHesapId,
        int DeviceId,
        int SecondDeviceId,
        string DeviceSerial);

    private sealed class FakePavoSaglayici : IPosOdemeSaglayicisi
    {
        public string Kod => "PAVO";
        public string Ad => "Pavo";
        public bool EslesmeDestekliyorMu => true;

        public void TerminalBilgileriniDogrula(PosTerminal terminal)
        {
            if (string.IsNullOrWhiteSpace(terminal.SerialNumber))
            {
                throw new BaseException("Terminal serial number boş olamaz.", 400);
            }
        }

        public Task<PosEslesmeSonucu> EslesmeBaslatAsync(PosTerminal terminal, CancellationToken cancellationToken) =>
            Task.FromResult(new PosEslesmeSonucu(123, "PAIR-123", "TARGET-FP", true));

        public Task<PosEslesmeSonucu> EslesmeKontrolAsync(PosTerminal terminal, CancellationToken cancellationToken) =>
            Task.FromResult(new PosEslesmeSonucu(123, "PAIR-123", "TARGET-FP", true));

        public Task<PosOdemeBaslatSonucu> OdemeBaslatAsync(PosTerminal terminal, string islemReferansi, decimal tutar, string paraBirimi, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PosOdemeSorguSonucu> OdemeDurumuAsync(PosTerminal terminal, string saglayiciIslemId, string islemReferansi, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
