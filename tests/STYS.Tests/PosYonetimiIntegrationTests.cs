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
using STYS.Rezervasyonlar;
using STYS.Tests.Agent;
using STYS.Tests.TestSupport;
using STYS.Rezervasyonlar.Entities;
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
    public async Task TerminalKaydetAsync_AyniTerminalIdFarkliAcquirerIleAyriTerminalOlusturur()
    {
        var cs = ConnectionString();
        if (cs is null) return;

        var suffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];
        await using var db = AgentTestSupport.CreateDbContext(cs);
        await db.Database.MigrateAsync();
        var fixture = await SeedAsync(db, suffix);

        var extraHesap = new KasaBankaHesap
        {
            TesisId = fixture.MainTesisId,
            Tip = KasaBankaHesapTipleri.KrediKarti,
            Kod = $"KK-{suffix}-X",
            Ad = $"Kredi Kartı {suffix} X",
            ParaBirimi = "TRY",
            AktifMi = true
        };
        db.Set<KasaBankaHesap>().Add(extraHesap);
        await db.SaveChangesAsync();

        var service = CreateTerminalService(db, fixture.KurumId);

        var first = await service.KaydetAsync(fixture.DeviceId, null, BuildTerminalRequest(fixture, fixture.MainKrediHesapId, suffix, "TERM-CANON"), CancellationToken.None);
        var second = await service.KaydetAsync(fixture.DeviceId, null, BuildTerminalRequest(fixture, extraHesap.Id, suffix, "TERM-CANON"), CancellationToken.None);

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal("TERM-CANON", first.TerminalId);
        Assert.Equal("TERM-CANON", second.TerminalId);
        Assert.NotEqual(first.AcquirerId, second.AcquirerId);

        var count = await db.PosTerminaller.IgnoreQueryFilters().CountAsync(x => x.PosCihaziId == fixture.DeviceId && !x.IsDeleted && x.SerialNumber == "TERM-CANON");
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
    public async Task PavoPairing_CommandUretir_SequenceArtirmadanPayloadCihazBazlidir()
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
        Assert.Equal(0, refreshed.TransactionSequence);

        var firstPayload = JsonSerializer.Deserialize<PavoPairingRequest>(first.Payload ?? string.Empty, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var secondPayload = JsonSerializer.Deserialize<PavoPingRequest>(second.Payload ?? string.Empty, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(firstPayload);
        Assert.NotNull(secondPayload);
        Assert.Equal(fixture.DeviceId, firstPayload!.PosCihaziId);
        Assert.Equal(0, firstPayload.TransactionHandle.TransactionSequence);
        Assert.Equal("127.0.0.1", firstPayload.IpAddress);
        Assert.Equal(fixture.DeviceId, secondPayload!.PosCihaziId);
        Assert.Equal(0, secondPayload.TransactionHandle.TransactionSequence);

        await CleanupAsync(db, suffix);
    }

    [IntegrationFact]
    public async Task PavoSequence_BackendPayloadSifirKaliyor_veRestartSonrasiDegismiyor()
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
        Assert.Equal(0, pingPayload!.TransactionHandle.TransactionSequence);
        Assert.Equal(0, infoPayload!.TransactionHandle.TransactionSequence);

        await using (var verifyDb = AgentTestSupport.CreateDbContext(cs))
        {
            var refreshed = await verifyDb.PosCihazlari.AsNoTracking().SingleAsync(x => x.Id == fixture.DeviceId);
            Assert.Equal(0, refreshed.TransactionSequence);
        }

        await using var db3 = AgentTestSupport.CreateDbContext(cs);
        var restartService = CreateCihazService(db3, cs, fixture.KurumId);
        var restartPing = await restartService.PingAsync(fixture.DeviceId, "test", CancellationToken.None);
        var restartPayload = JsonSerializer.Deserialize<PavoPingRequest>(restartPing.Payload ?? string.Empty, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(restartPayload);
        Assert.Equal(0, restartPayload!.TransactionHandle.TransactionSequence);

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
        var leaseToken = (await agentService.GetPendingCommandsAsync(fixture.MainAgentId, CancellationToken.None)).Single().LeaseToken!;
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
            LeaseToken = leaseToken,
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

        Assert.Equal(2, terminals.Count);
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
        var leaseToken = (await agentService.GetPendingCommandsAsync(fixture.MainAgentId, CancellationToken.None)).Single().LeaseToken!;
        await agentService.AcceptAsync(command.Id, fixture.MainAgentId, CancellationToken.None);
        await agentService.SetRunningAsync(command.Id, fixture.MainAgentId, CancellationToken.None);

        var deviceRow = await db.PosCihazlari.FirstAsync(x => x.Id == fixture.DeviceId);
        deviceRow.AgentId = fixture.OtherTesisAgentId;
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<BaseException>(() => agentService.CompleteAsync(command.Id, fixture.MainAgentId, new AgentCommandCompleteRequest
        {
            Id = command.Id,
            Success = true,
            LeaseToken = leaseToken,
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
        var leaseToken = (await agentService.GetPendingCommandsAsync(fixture.MainAgentId, CancellationToken.None)).Single().LeaseToken!;
        await agentService.AcceptAsync(command.Id, fixture.MainAgentId, CancellationToken.None);
        await agentService.SetRunningAsync(command.Id, fixture.MainAgentId, CancellationToken.None);

        var deviceRow = await db.PosCihazlari.FirstAsync(x => x.Id == fixture.DeviceId);
        deviceRow.KurumId = fixture.KurumId + 999999;
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<BaseException>(() => agentService.CompleteAsync(command.Id, fixture.MainAgentId, new AgentCommandCompleteRequest
        {
            Id = command.Id,
            Success = true,
            LeaseToken = leaseToken,
            ResultPayload = JsonSerializer.Serialize(new PavoGetDeviceInfoResponse(), new JsonSerializerOptions(JsonSerializerDefaults.Web))
        }, CancellationToken.None));

        await CleanupAsync(db, suffix);
    }

    [IntegrationFact]
    public async Task Readiness_AgentOffline_NotReady()
    {
        var cs = ConnectionString();
        if (cs is null) return;

        var suffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];
        await using var db = AgentTestSupport.CreateDbContext(cs);
        await db.Database.MigrateAsync();
        var fixture = await SeedAsync(db, suffix);

        var agent = await db.Set<AgentEntity>().FirstAsync(x => x.Id == fixture.MainAgentId);
        agent.LastHeartbeatAt = DateTime.UtcNow.AddMinutes(-10);
        var device = await db.Set<PosCihazi>().FirstAsync(x => x.Id == fixture.DeviceId);
        SetProvisionedDeviceHealth(device, suffix, PavoDeviceHealthStatus.Stale, DateTime.UtcNow.AddMinutes(-20), DateTime.UtcNow.AddMinutes(-20), "stale");
        await db.SaveChangesAsync();

        var service = CreateCihazService(db, cs, fixture.KurumId);
        var readiness = await service.GetReadinessAsync(fixture.DeviceId, CancellationToken.None);

        Assert.Equal(PavoOperationalReadiness.AgentOffline, readiness.Status);
        Assert.Equal(PavoDeviceHealthStatus.Stale, readiness.DeviceHealthStatus);
        Assert.False(readiness.Ready);
        Assert.Equal("Agent çevrimdışı.", readiness.LastError);

        await CleanupAsync(db, suffix);
    }

    [IntegrationFact]
    public async Task Readiness_DisabledDevice_NotReady()
    {
        var cs = ConnectionString();
        if (cs is null) return;

        var suffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];
        await using var db = AgentTestSupport.CreateDbContext(cs);
        await db.Database.MigrateAsync();
        var fixture = await SeedAsync(db, suffix);

        var agent = await db.Set<AgentEntity>().FirstAsync(x => x.Id == fixture.MainAgentId);
        agent.LastHeartbeatAt = DateTime.UtcNow;
        var device = await db.Set<PosCihazi>().FirstAsync(x => x.Id == fixture.DeviceId);
        device.AktifMi = false;
        device.AgentLocalDeviceId = $"LOCAL-{suffix}";
        device.Fingerprint = $"FP-{suffix}";
        device.TargetFingerprint = $"FP-{suffix}";
        device.EslesmeOnayliMi = true;
        await db.SaveChangesAsync();

        var service = CreateCihazService(db, cs, fixture.KurumId);
        var readiness = await service.GetReadinessAsync(fixture.DeviceId, CancellationToken.None);

        Assert.Equal(PavoOperationalReadiness.Disabled, readiness.Status);
        Assert.Equal(PavoDeviceHealthStatus.Unknown, readiness.DeviceHealthStatus);
        Assert.False(readiness.Ready);
        Assert.Equal("POS cihazı devre dışı.", readiness.LastError);

        await CleanupAsync(db, suffix);
    }

    [IntegrationFact]
    public async Task Readiness_FingerprintTargetMismatch_Ready()
    {
        var cs = ConnectionString();
        if (cs is null) return;

        var suffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];
        await using var db = AgentTestSupport.CreateDbContext(cs);
        await db.Database.MigrateAsync();
        var fixture = await SeedAsync(db, suffix);

        var agent = await db.Set<AgentEntity>().FirstAsync(x => x.Id == fixture.MainAgentId);
        agent.LastHeartbeatAt = DateTime.UtcNow;
        var device = await db.Set<PosCihazi>().FirstAsync(x => x.Id == fixture.DeviceId);
        device.SonBaglantiTarihi = DateTime.UtcNow;
        device.AgentLocalDeviceId = $"LOCAL-{suffix}";
        device.Fingerprint = $"LOCAL-FP-{suffix}";
        device.TargetFingerprint = $"REMOTE-FP-{suffix}";
        device.EslesmeOnayliMi = true;
        await db.SaveChangesAsync();

        var service = CreateCihazService(db, cs, fixture.KurumId);
        var readiness = await service.GetReadinessAsync(fixture.DeviceId, CancellationToken.None);

        Assert.Equal(PavoOperationalReadiness.Ready, readiness.Status);
        Assert.True(readiness.Ready);

        await CleanupAsync(db, suffix);
    }

    [IntegrationFact]
    public async Task Readiness_PairingInvalid_NotReady()
    {
        var cs = ConnectionString();
        if (cs is null) return;

        var suffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];
        await using var db = AgentTestSupport.CreateDbContext(cs);
        await db.Database.MigrateAsync();
        var fixture = await SeedAsync(db, suffix);

        var agent = await db.Set<AgentEntity>().FirstAsync(x => x.Id == fixture.MainAgentId);
        agent.LastHeartbeatAt = DateTime.UtcNow;
        var device = await db.Set<PosCihazi>().FirstAsync(x => x.Id == fixture.DeviceId);
        device.SonBaglantiTarihi = DateTime.UtcNow;
        device.AgentLocalDeviceId = $"LOCAL-{suffix}";
        device.Fingerprint = null;
        device.TargetFingerprint = null;
        device.EslesmeOnayliMi = false;
        await db.SaveChangesAsync();

        var service = CreateCihazService(db, cs, fixture.KurumId);
        var readiness = await service.GetReadinessAsync(fixture.DeviceId, CancellationToken.None);

        Assert.Equal(PavoOperationalReadiness.PairingInvalid, readiness.Status);
        Assert.False(readiness.Ready);

        await CleanupAsync(db, suffix);
    }

    [IntegrationFact]
    public async Task Readiness_NoActiveTerminal_NotReady()
    {
        var cs = ConnectionString();
        if (cs is null) return;

        var suffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];
        await using var db = AgentTestSupport.CreateDbContext(cs);
        await db.Database.MigrateAsync();
        var fixture = await SeedAsync(db, suffix);

        var agent = await db.Set<AgentEntity>().FirstAsync(x => x.Id == fixture.MainAgentId);
        agent.LastHeartbeatAt = DateTime.UtcNow;
        var device = await db.Set<PosCihazi>().FirstAsync(x => x.Id == fixture.DeviceId);
        device.SonBaglantiTarihi = DateTime.UtcNow;
        device.AgentLocalDeviceId = $"LOCAL-{suffix}";
        device.Fingerprint = $"FP-{suffix}";
        device.TargetFingerprint = $"FP-{suffix}";
        device.EslesmeOnayliMi = true;
        await db.SaveChangesAsync();

        var service = CreateCihazService(db, cs, fixture.KurumId);
        var readiness = await service.GetReadinessAsync(fixture.DeviceId, CancellationToken.None);

        Assert.Equal(PavoOperationalReadiness.NoActiveTerminal, readiness.Status);
        Assert.False(readiness.Ready);
        Assert.Equal("Aktif terminal bulunamadı.", readiness.LastError);

        await CleanupAsync(db, suffix);
    }

    [IntegrationFact]
    public async Task Readiness_NoAccountMapping_NotReady()
    {
        var cs = ConnectionString();
        if (cs is null) return;

        var suffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];
        await using var db = AgentTestSupport.CreateDbContext(cs);
        await db.Database.MigrateAsync();
        var fixture = await SeedAsync(db, suffix);

        var agent = await db.Set<AgentEntity>().FirstAsync(x => x.Id == fixture.MainAgentId);
        agent.LastHeartbeatAt = DateTime.UtcNow;
        var device = await db.Set<PosCihazi>().FirstAsync(x => x.Id == fixture.DeviceId);
        device.SonBaglantiTarihi = DateTime.UtcNow;
        device.AgentLocalDeviceId = $"LOCAL-{suffix}";
        device.Fingerprint = $"FP-{suffix}";
        device.TargetFingerprint = $"FP-{suffix}";
        device.EslesmeOnayliMi = true;
        await db.SaveChangesAsync();

        var terminalService = CreateTerminalService(db, fixture.KurumId);
        await terminalService.KaydetAsync(fixture.DeviceId, null, BuildTerminalRequest(fixture, null, suffix, "READY-NO-ACC"), CancellationToken.None);

        var service = CreateCihazService(db, cs, fixture.KurumId);
        var readiness = await service.GetReadinessAsync(fixture.DeviceId, CancellationToken.None);

        Assert.Equal(PavoOperationalReadiness.NoAccountMapping, readiness.Status);
        Assert.False(readiness.Ready);
        Assert.Equal("Aktif terminal için kredi kartı hesabı eşleştirilmemiş.", readiness.LastError);

        await CleanupAsync(db, suffix);
    }

    [IntegrationFact]
    public async Task Readiness_FullyProvisioned_Ready_AndResponseContainsNoFingerprint()
    {
        var cs = ConnectionString();
        if (cs is null) return;

        var suffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];
        await using var db = AgentTestSupport.CreateDbContext(cs);
        await db.Database.MigrateAsync();
        var fixture = await SeedAsync(db, suffix);

        var agent = await db.Set<AgentEntity>().FirstAsync(x => x.Id == fixture.MainAgentId);
        agent.LastHeartbeatAt = DateTime.UtcNow;
        var device = await db.Set<PosCihazi>().FirstAsync(x => x.Id == fixture.DeviceId);
        device.SonBaglantiTarihi = DateTime.UtcNow;
        device.AgentLocalDeviceId = $"LOCAL-{suffix}";
        device.Fingerprint = $"FP-{suffix}";
        device.TargetFingerprint = $"FP-{suffix}";
        device.EslesmeOnayliMi = true;
        await db.SaveChangesAsync();

        var terminalService = CreateTerminalService(db, fixture.KurumId);
        await terminalService.KaydetAsync(fixture.DeviceId, null, BuildTerminalRequest(fixture, fixture.MainKrediHesapId, suffix, "READY-OK"), CancellationToken.None);

        var service = CreateCihazService(db, cs, fixture.KurumId);
        var readiness = await service.GetReadinessAsync(fixture.DeviceId, CancellationToken.None);

        var json = JsonSerializer.Serialize(readiness, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(PavoOperationalReadiness.Ready, readiness.Status);
        Assert.True(readiness.Ready);
        Assert.Null(readiness.LastError);
        Assert.Contains("\"ready\":true", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fingerprint", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("targetFingerprint", json, StringComparison.OrdinalIgnoreCase);

        await CleanupAsync(db, suffix);
    }

    [IntegrationFact]
    public async Task Readiness_FreshHealthy_Ready()
    {
        var cs = ConnectionString();
        if (cs is null) return;

        var suffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];
        await using var db = AgentTestSupport.CreateDbContext(cs);
        await db.Database.MigrateAsync();
        var fixture = await SeedAsync(db, suffix);

        var agent = await db.Set<AgentEntity>().FirstAsync(x => x.Id == fixture.MainAgentId);
        agent.LastHeartbeatAt = DateTime.UtcNow;
        var device = await db.Set<PosCihazi>().FirstAsync(x => x.Id == fixture.DeviceId);
        SetHealthyDeviceHealth(device, suffix);
        await db.SaveChangesAsync();

        var terminalService = CreateTerminalService(db, fixture.KurumId);
        await terminalService.KaydetAsync(fixture.DeviceId, null, BuildTerminalRequest(fixture, fixture.MainKrediHesapId, suffix, "HEALTH-READY"), CancellationToken.None);

        var service = CreateCihazService(db, cs, fixture.KurumId);
        var readiness = await service.GetReadinessAsync(fixture.DeviceId, CancellationToken.None);

        Assert.True(readiness.Ready);
        Assert.Equal(PavoDeviceHealthStatus.Healthy, readiness.DeviceHealthStatus);
        Assert.Equal("Healthy", readiness.LastHealthStatus);

        await CleanupAsync(db, suffix);
    }

    [IntegrationFact]
    public async Task Readiness_StaleHealth_NotReady()
    {
        var cs = ConnectionString();
        if (cs is null) return;

        var suffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];
        await using var db = AgentTestSupport.CreateDbContext(cs);
        await db.Database.MigrateAsync();
        var fixture = await SeedAsync(db, suffix);

        var agent = await db.Set<AgentEntity>().FirstAsync(x => x.Id == fixture.MainAgentId);
        agent.LastHeartbeatAt = DateTime.UtcNow;
        var device = await db.Set<PosCihazi>().FirstAsync(x => x.Id == fixture.DeviceId);
        SetProvisionedDeviceHealth(device, suffix, PavoDeviceHealthStatus.Healthy, DateTime.UtcNow.AddMinutes(-20), DateTime.UtcNow.AddMinutes(-20));
        await db.SaveChangesAsync();

        var terminalService = CreateTerminalService(db, fixture.KurumId);
        await terminalService.KaydetAsync(fixture.DeviceId, null, BuildTerminalRequest(fixture, fixture.MainKrediHesapId, suffix, "HEALTH-STALE"), CancellationToken.None);

        var service = CreateCihazService(db, cs, fixture.KurumId);
        var readiness = await service.GetReadinessAsync(fixture.DeviceId, CancellationToken.None);

        Assert.Equal(PavoOperationalReadiness.DeviceOffline, readiness.Status);
        Assert.Equal(PavoDeviceHealthStatus.Stale, readiness.DeviceHealthStatus);
        Assert.False(readiness.Ready);
        Assert.Equal("Son başarılı PAVO sağlık kontrolü eski.", readiness.LastError);

        await CleanupAsync(db, suffix);
    }

    [IntegrationFact]
    public async Task Readiness_TimeoutHealth_NotReady()
    {
        var cs = ConnectionString();
        if (cs is null) return;

        var suffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];
        await using var db = AgentTestSupport.CreateDbContext(cs);
        await db.Database.MigrateAsync();
        var fixture = await SeedAsync(db, suffix);

        var agent = await db.Set<AgentEntity>().FirstAsync(x => x.Id == fixture.MainAgentId);
        agent.LastHeartbeatAt = DateTime.UtcNow;
        var device = await db.Set<PosCihazi>().FirstAsync(x => x.Id == fixture.DeviceId);
        SetProvisionedDeviceHealth(device, suffix, PavoDeviceHealthStatus.Timeout, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(-1), "timeout");
        await db.SaveChangesAsync();

        var terminalService = CreateTerminalService(db, fixture.KurumId);
        await terminalService.KaydetAsync(fixture.DeviceId, null, BuildTerminalRequest(fixture, fixture.MainKrediHesapId, suffix, "HEALTH-TIMEOUT"), CancellationToken.None);

        var service = CreateCihazService(db, cs, fixture.KurumId);
        var readiness = await service.GetReadinessAsync(fixture.DeviceId, CancellationToken.None);

        Assert.Equal(PavoOperationalReadiness.DeviceOffline, readiness.Status);
        Assert.Equal(PavoDeviceHealthStatus.Timeout, readiness.DeviceHealthStatus);
        Assert.False(readiness.Ready);
        Assert.Equal("PAVO sağlık kontrolü zaman aşımına uğradı.", readiness.LastError);

        await CleanupAsync(db, suffix);
    }

    [IntegrationFact]
    public async Task Readiness_UnreachableHealth_NotReady()
    {
        var cs = ConnectionString();
        if (cs is null) return;

        var suffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];
        await using var db = AgentTestSupport.CreateDbContext(cs);
        await db.Database.MigrateAsync();
        var fixture = await SeedAsync(db, suffix);

        var agent = await db.Set<AgentEntity>().FirstAsync(x => x.Id == fixture.MainAgentId);
        agent.LastHeartbeatAt = DateTime.UtcNow;
        var device = await db.Set<PosCihazi>().FirstAsync(x => x.Id == fixture.DeviceId);
        SetProvisionedDeviceHealth(device, suffix, PavoDeviceHealthStatus.Unreachable, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(-1), "unreachable");
        await db.SaveChangesAsync();

        var terminalService = CreateTerminalService(db, fixture.KurumId);
        await terminalService.KaydetAsync(fixture.DeviceId, null, BuildTerminalRequest(fixture, fixture.MainKrediHesapId, suffix, "HEALTH-UNREACH"), CancellationToken.None);

        var service = CreateCihazService(db, cs, fixture.KurumId);
        var readiness = await service.GetReadinessAsync(fixture.DeviceId, CancellationToken.None);

        Assert.Equal(PavoOperationalReadiness.DeviceOffline, readiness.Status);
        Assert.Equal(PavoDeviceHealthStatus.Unreachable, readiness.DeviceHealthStatus);
        Assert.False(readiness.Ready);
        Assert.Equal("PAVO cihazına ulaşılamıyor.", readiness.LastError);

        await CleanupAsync(db, suffix);
    }

    [IntegrationFact]
    public async Task Ping_SuccessUpdatesCentralHealth()
    {
        var cs = ConnectionString();
        if (cs is null) return;

        var suffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];
        await using var db = AgentTestSupport.CreateDbContext(cs);
        await db.Database.MigrateAsync();
        var fixture = await SeedAsync(db, suffix);

        var device = await db.Set<PosCihazi>().FirstAsync(x => x.Id == fixture.DeviceId);
        SetProvisionedDeviceHealth(device, suffix, PavoDeviceHealthStatus.Unreachable, DateTime.UtcNow.AddMinutes(-10), DateTime.UtcNow.AddMinutes(-10), "initial");
        await db.SaveChangesAsync();

        var service = CreateCihazService(db, cs, fixture.KurumId);
        var command = await service.PingAsync(fixture.DeviceId, "test", CancellationToken.None);
        var agentService = CreateAgentCommandService(cs, fixture.KurumId);
        var leaseToken = (await agentService.GetPendingCommandsAsync(fixture.MainAgentId, CancellationToken.None)).Single().LeaseToken!;

        await agentService.AcceptAsync(command.Id, fixture.MainAgentId, CancellationToken.None);
        await agentService.SetRunningAsync(command.Id, fixture.MainAgentId, CancellationToken.None);
        await agentService.CompleteAsync(command.Id, fixture.MainAgentId, new AgentCommandCompleteRequest
        {
            Id = command.Id,
            Success = true,
            LeaseToken = leaseToken,
            ResultPayload = JsonSerializer.Serialize(new PavoPingResponse(), new JsonSerializerOptions(JsonSerializerDefaults.Web))
        }, CancellationToken.None);

        var updatedDevice = await db.Set<PosCihazi>().AsNoTracking().FirstAsync(x => x.Id == fixture.DeviceId);
        Assert.Equal(PavoDeviceHealthStatus.Healthy, updatedDevice.LastHealthStatus);
        Assert.NotNull(updatedDevice.LastHealthCheckAt);
        Assert.NotNull(updatedDevice.LastHealthSuccessAt);
        Assert.Null(updatedDevice.LastHealthError);

        await CleanupAsync(db, suffix);
    }

    [IntegrationFact]
    public async Task Ping_FailurePreservesLastSuccessAt()
    {
        var cs = ConnectionString();
        if (cs is null) return;

        var suffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];
        await using var db = AgentTestSupport.CreateDbContext(cs);
        await db.Database.MigrateAsync();
        var fixture = await SeedAsync(db, suffix);

        var device = await db.Set<PosCihazi>().FirstAsync(x => x.Id == fixture.DeviceId);
        var previousSuccess = DateTime.UtcNow.AddMinutes(-3);
        SetHealthyDeviceHealth(device, suffix, previousSuccess, previousSuccess);
        await db.SaveChangesAsync();

        var service = CreateCihazService(db, cs, fixture.KurumId);
        var command = await service.PingAsync(fixture.DeviceId, "test", CancellationToken.None);
        var agentService = CreateAgentCommandService(cs, fixture.KurumId);
        var leaseToken = (await agentService.GetPendingCommandsAsync(fixture.MainAgentId, CancellationToken.None)).Single().LeaseToken!;

        await agentService.AcceptAsync(command.Id, fixture.MainAgentId, CancellationToken.None);
        await agentService.SetRunningAsync(command.Id, fixture.MainAgentId, CancellationToken.None);
        await agentService.CompleteAsync(command.Id, fixture.MainAgentId, new AgentCommandCompleteRequest
        {
            Id = command.Id,
            Success = false,
            LeaseToken = leaseToken,
            ErrorCode = "TIMEOUT",
            ErrorMessage = "timeout"
        }, CancellationToken.None);

        var updatedDevice = await db.Set<PosCihazi>().AsNoTracking().FirstAsync(x => x.Id == fixture.DeviceId);
        Assert.Equal(previousSuccess, updatedDevice.LastHealthSuccessAt);
        Assert.Equal(PavoDeviceHealthStatus.Timeout, updatedDevice.LastHealthStatus);
        Assert.NotNull(updatedDevice.LastHealthError);

        await CleanupAsync(db, suffix);
    }

    [IntegrationFact]
    public async Task Ping_DuplicateActiveCommandReturnsExisting()
    {
        var cs = ConnectionString();
        if (cs is null) return;

        var suffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];
        await using var db = AgentTestSupport.CreateDbContext(cs);
        await db.Database.MigrateAsync();
        var fixture = await SeedAsync(db, suffix);

        var device = await db.Set<PosCihazi>().FirstAsync(x => x.Id == fixture.DeviceId);
        var previousSuccess = DateTime.UtcNow.AddMinutes(-5);
        SetHealthyDeviceHealth(device, suffix, previousSuccess, previousSuccess);
        await db.SaveChangesAsync();

        var service = CreateCihazService(db, cs, fixture.KurumId);
        var first = await service.PingAsync(fixture.DeviceId, "test", CancellationToken.None);
        var second = await service.PingAsync(fixture.DeviceId, "test", CancellationToken.None);

        Assert.Equal(first.Id, second.Id);
        var commandCount = await db.Set<STYS.Agent.Entities.AgentCommand>().CountAsync(x => x.AgentId == fixture.MainAgentId && x.CommandType == "PavoPing" && !x.IsDeleted);
        Assert.Equal(1, commandCount);

        await CleanupAsync(db, suffix);
    }

    [IntegrationFact]
    public async Task RunningPavoPing_ExpiresWithoutAgentPolling()
    {
        var cs = ConnectionString();
        if (cs is null) return;

        var suffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];
        await using var db = AgentTestSupport.CreateDbContext(cs);
        await db.Database.MigrateAsync();
        var fixture = await SeedAsync(db, suffix);

        var device = await db.Set<PosCihazi>().FirstAsync(x => x.Id == fixture.DeviceId);
        var previousSuccess = DateTime.UtcNow.AddMinutes(-5);
        SetHealthyDeviceHealth(device, suffix, previousSuccess, previousSuccess);
        await db.SaveChangesAsync();

        var commandId = Guid.NewGuid();
        db.Set<STYS.Agent.Entities.AgentCommand>().Add(new STYS.Agent.Entities.AgentCommand
        {
            Id = commandId,
            AgentId = fixture.MainAgentId,
            KurumId = fixture.KurumId,
            CommandType = "PavoPing",
            Payload = JsonSerializer.Serialize(new PavoPingRequest { PosCihaziId = fixture.DeviceId }, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            Status = AgentCommandStatus.Running,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1),
            Priority = 1,
            CorrelationId = Guid.NewGuid().ToString("N"),
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            RequestedBy = "test",
            CreatedBy = "test",
            CreatedAt = DateTime.UtcNow.AddMinutes(-5)
        });
        await db.SaveChangesAsync();

        var expiryService = CreateCommandExpiryService(cs);
        await expiryService.ExpireTimedOutCommandsAsync(fixture.MainAgentId, CancellationToken.None);

        var expired = await db.Set<STYS.Agent.Entities.AgentCommand>().AsNoTracking().FirstAsync(x => x.Id == commandId);
        var updatedDevice = await db.Set<PosCihazi>().AsNoTracking().FirstAsync(x => x.Id == fixture.DeviceId);

        Assert.Equal(AgentCommandStatus.Expired, expired.Status);
        Assert.Equal(PavoDeviceHealthStatus.Timeout, updatedDevice.LastHealthStatus);
        Assert.NotNull(updatedDevice.LastHealthCheckAt);
        Assert.Equal(previousSuccess, updatedDevice.LastHealthSuccessAt);
        Assert.Equal("PAVO sağlık kontrolü zaman aşımına uğradı.", updatedDevice.LastHealthError);

        await CleanupAsync(db, suffix);
    }

    [IntegrationFact]
    public async Task ExpiredRunningPing_NewPingUretiminiEngellemez()
    {
        var cs = ConnectionString();
        if (cs is null) return;

        var suffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];
        await using var db = AgentTestSupport.CreateDbContext(cs);
        await db.Database.MigrateAsync();
        var fixture = await SeedAsync(db, suffix);

        var device = await db.Set<PosCihazi>().FirstAsync(x => x.Id == fixture.DeviceId);
        SetHealthyDeviceHealth(device, suffix, DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddMinutes(-5));
        await db.SaveChangesAsync();

        var expiredCommandId = Guid.NewGuid();
        db.Set<STYS.Agent.Entities.AgentCommand>().Add(new STYS.Agent.Entities.AgentCommand
        {
            Id = expiredCommandId,
            AgentId = fixture.MainAgentId,
            KurumId = fixture.KurumId,
            CommandType = "PavoPing",
            Payload = JsonSerializer.Serialize(new PavoPingRequest { PosCihaziId = fixture.DeviceId }, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            Status = AgentCommandStatus.Running,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1),
            Priority = 1,
            CorrelationId = Guid.NewGuid().ToString("N"),
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            RequestedBy = "test",
            CreatedBy = "test",
            CreatedAt = DateTime.UtcNow.AddMinutes(-5)
        });
        await db.SaveChangesAsync();

        var service = CreateCihazService(db, cs, fixture.KurumId);
        var command = await service.PingAsync(fixture.DeviceId, "test", CancellationToken.None);

        Assert.NotEqual(expiredCommandId, command.Id);
        Assert.Equal("PavoPing", command.CommandType);

        var expired = await db.Set<STYS.Agent.Entities.AgentCommand>().AsNoTracking().FirstAsync(x => x.Id == expiredCommandId);
        Assert.Equal(AgentCommandStatus.Expired, expired.Status);

        var commandCount = await db.Set<STYS.Agent.Entities.AgentCommand>().CountAsync(x => x.AgentId == fixture.MainAgentId && x.CommandType == "PavoPing" && !x.IsDeleted);
        Assert.Equal(2, commandCount);

        await CleanupAsync(db, suffix);
    }

    [IntegrationFact]
    public async Task Payment_NotReady_ByHealth_DoesNotCreateCommandOrReserveSequence()
    {
        var cs = ConnectionString();
        if (cs is null) return;

        var suffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];
        await using var db = AgentTestSupport.CreateDbContext(cs);
        await db.Database.MigrateAsync();
        var fixture = await SeedAsync(db, suffix);

        var device = await db.Set<PosCihazi>().FirstAsync(x => x.Id == fixture.DeviceId);
        SetProvisionedDeviceHealth(device, suffix, PavoDeviceHealthStatus.Stale, DateTime.UtcNow.AddMinutes(-15), DateTime.UtcNow.AddMinutes(-15), "stale");
        device.TransactionSequence = 7;
        await db.SaveChangesAsync();

        var terminalService = CreateTerminalService(db, fixture.KurumId);
        var terminal = await terminalService.KaydetAsync(fixture.DeviceId, null, BuildTerminalRequest(fixture, fixture.MainKrediHesapId, suffix, "PAY-HEALTH"), CancellationToken.None);
        var paymentService = CreatePaymentService(db, cs, fixture.KurumId);

        await Assert.ThrowsAsync<BaseException>(() => paymentService.StartAsync(fixture.DeviceId, new PosPaymentBaslatRequest
        {
            PosTerminalId = terminal.Id,
            Tutar = 1.00m,
            ParaBirimi = "TRY",
            Aciklama = "integration",
            IdempotencyKey = NewPaymentKey()
        }, "test", CancellationToken.None));

        var commandCount = await db.Set<STYS.Agent.Entities.AgentCommand>().CountAsync(x => x.AgentId == fixture.MainAgentId && x.CommandType == "PavoStartPayment");
        var reloadedDevice = await db.Set<PosCihazi>().AsNoTracking().FirstAsync(x => x.Id == fixture.DeviceId);

        Assert.Equal(0, commandCount);
        Assert.Equal(7, reloadedDevice.TransactionSequence);

        await CleanupAsync(db, suffix);
    }

    [IntegrationFact]
    public async Task Payment_NotReady_StartPaymentDoesNotCreateCommandOrReserveSequence()
    {
        var cs = ConnectionString();
        if (cs is null) return;

        var suffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];
        await using var db = AgentTestSupport.CreateDbContext(cs);
        await db.Database.MigrateAsync();
        var fixture = await SeedAsync(db, suffix);

        var device = await db.Set<PosCihazi>().FirstAsync(x => x.Id == fixture.DeviceId);
        device.TransactionSequence = 7;
        device.SonBaglantiTarihi = DateTime.UtcNow;
        device.AgentLocalDeviceId = $"LOCAL-{suffix}";
        device.Fingerprint = $"FP-{suffix}";
        device.TargetFingerprint = $"FP-{suffix}";
        device.EslesmeOnayliMi = true;
        await db.SaveChangesAsync();

        var terminalService = CreateTerminalService(db, fixture.KurumId);
        var terminal = await terminalService.KaydetAsync(fixture.DeviceId, null, BuildTerminalRequest(fixture, null, suffix, "PAY-NOT-READY"), CancellationToken.None);
        var paymentService = CreatePaymentService(db, cs, fixture.KurumId);

        await Assert.ThrowsAsync<BaseException>(() => paymentService.StartAsync(fixture.DeviceId, new PosPaymentBaslatRequest
        {
            PosTerminalId = terminal.Id,
            Tutar = 1.00m,
            ParaBirimi = "TRY",
            Aciklama = "integration",
            IdempotencyKey = NewPaymentKey()
        }, "test", CancellationToken.None));

        var commandCount = await db.Set<STYS.Agent.Entities.AgentCommand>().CountAsync(x => x.AgentId == fixture.MainAgentId && x.CommandType == "PavoStartPayment");
        var paymentCount = await db.PosOdemeIslemleri.CountAsync(x => x.PosCihaziId == fixture.DeviceId);
        var reloadedDevice = await db.Set<PosCihazi>().AsNoTracking().FirstAsync(x => x.Id == fixture.DeviceId);

        Assert.Equal(0, commandCount);
        Assert.Equal(0, paymentCount);
        Assert.Equal(7, reloadedDevice.TransactionSequence);

        await CleanupAsync(db, suffix);
    }

    [IntegrationFact]
    public async Task Payment_StartPaymentCommandOlusturur()
    {
        var cs = ConnectionString();
        if (cs is null) return;

        var suffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];
        await using var db = AgentTestSupport.CreateDbContext(cs);
        await db.Database.MigrateAsync();
        var fixture = await SeedAsync(db, suffix);
        var terminalService = CreateTerminalService(db, fixture.KurumId);
        var terminal = await terminalService.KaydetAsync(fixture.DeviceId, null, BuildTerminalRequest(fixture, fixture.MainKrediHesapId, suffix, "PAY-1"), CancellationToken.None);
        var paymentService = CreatePaymentService(db, cs, fixture.KurumId);
        var paymentKey = NewPaymentKey();

        var payment = await paymentService.StartAsync(fixture.DeviceId, new PosPaymentBaslatRequest
        {
            PosTerminalId = terminal.Id,
            Tutar = 1.00m,
            ParaBirimi = "TRY",
            Aciklama = "integration",
            IdempotencyKey = paymentKey
        }, "test", CancellationToken.None);

        Assert.NotNull(payment.AgentCommandId);
        Assert.NotNull(payment.SaleReference);
        Assert.Equal(PosOdemeDurumlari.SentToAgent, payment.Durum);

        var command = await db.Set<STYS.Agent.Entities.AgentCommand>().FirstAsync(x => x.Id == payment.AgentCommandId);
        Assert.Equal("PavoStartPayment", command.CommandType);

        await CleanupAsync(db, suffix);
    }

    [IntegrationFact]
    public async Task Payment_TerminalWithoutAccountEngellenir()
    {
        var cs = ConnectionString();
        if (cs is null) return;

        var suffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];
        await using var db = AgentTestSupport.CreateDbContext(cs);
        await db.Database.MigrateAsync();
        var fixture = await SeedAsync(db, suffix);
        var terminalService = CreateTerminalService(db, fixture.KurumId);
        var terminal = await terminalService.KaydetAsync(fixture.DeviceId, null, BuildTerminalRequest(fixture, null, suffix, "PAY-NO-ACC"), CancellationToken.None);
        var paymentService = CreatePaymentService(db, cs, fixture.KurumId);
        var paymentKey = NewPaymentKey();

        await Assert.ThrowsAsync<BaseException>(() => paymentService.StartAsync(fixture.DeviceId, new PosPaymentBaslatRequest
        {
            PosTerminalId = terminal.Id,
            Tutar = 1.00m,
            ParaBirimi = "TRY",
            Aciklama = "integration",
            IdempotencyKey = paymentKey
        }, "test", CancellationToken.None));

        await CleanupAsync(db, suffix);
    }

    [IntegrationFact]
    public async Task Payment_CrossKurumEngellenir()
    {
        var cs = ConnectionString();
        if (cs is null) return;

        var suffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];
        await using var db = AgentTestSupport.CreateDbContext(cs);
        await db.Database.MigrateAsync();
        var fixture = await SeedAsync(db, suffix);
        var paymentService = CreatePaymentService(db, cs, fixture.KurumId);
        var paymentKey = NewPaymentKey();

        db.Set<PosTerminal>().Add(new PosTerminal
        {
            KurumId = fixture.KurumId + 999999,
            TesisId = fixture.OtherKurumTesisId,
            PosCihaziId = fixture.DeviceId,
            KasaBankaHesapId = fixture.OtherKurumHesapId,
            SaglayiciKodu = "PAVO",
            Ad = $"Cross-{suffix}",
            SerialNumber = $"X-{suffix}",
            SourceTerminalReference = $"X-{suffix}",
            AktifMi = true,
            CreatedBy = "test",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var terminal = await db.Set<PosTerminal>().OrderByDescending(x => x.Id).FirstAsync();

        await Assert.ThrowsAsync<BaseException>(() => paymentService.StartAsync(fixture.DeviceId, new PosPaymentBaslatRequest
        {
            PosTerminalId = terminal.Id,
            Tutar = 1.00m,
            ParaBirimi = "TRY",
            Aciklama = "integration",
            IdempotencyKey = paymentKey
        }, "test", CancellationToken.None));

        await CleanupAsync(db, suffix);
    }

    [IntegrationFact]
    public async Task Payment_WrongAgentEngellenir()
    {
        var cs = ConnectionString();
        if (cs is null) return;

        var suffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];
        await using var db = AgentTestSupport.CreateDbContext(cs);
        await db.Database.MigrateAsync();
        var fixture = await SeedAsync(db, suffix);
        var terminalService = CreateTerminalService(db, fixture.KurumId);
        var terminal = await terminalService.KaydetAsync(fixture.DeviceId, null, BuildTerminalRequest(fixture, fixture.MainKrediHesapId, suffix, "PAY-AG"), CancellationToken.None);
        var paymentService = CreatePaymentService(db, cs, fixture.KurumId);
        var paymentKey = NewPaymentKey();

        var device = await db.Set<PosCihazi>().FirstAsync(x => x.Id == fixture.DeviceId);
        device.AgentId = fixture.OtherTesisAgentId;
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<BaseException>(() => paymentService.StartAsync(fixture.DeviceId, new PosPaymentBaslatRequest
        {
            PosTerminalId = terminal.Id,
            Tutar = 1.00m,
            ParaBirimi = "TRY",
            Aciklama = "integration",
            IdempotencyKey = paymentKey
        }, "test", CancellationToken.None));

        await CleanupAsync(db, suffix);
    }

    [IntegrationFact]
    public async Task Payment_SaleReferenceRetrySameKalir()
    {
        var cs = ConnectionString();
        if (cs is null) return;

        var suffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];
        await using var db = AgentTestSupport.CreateDbContext(cs);
        await db.Database.MigrateAsync();
        var fixture = await SeedAsync(db, suffix);
        var terminalService = CreateTerminalService(db, fixture.KurumId);
        var terminal = await terminalService.KaydetAsync(fixture.DeviceId, null, BuildTerminalRequest(fixture, fixture.MainKrediHesapId, suffix, "PAY-RETRY"), CancellationToken.None);
        var paymentService = CreatePaymentService(db, cs, fixture.KurumId);
        var paymentKey = NewPaymentKey();

        var first = await paymentService.StartAsync(fixture.DeviceId, new PosPaymentBaslatRequest
        {
            PosTerminalId = terminal.Id,
            Tutar = 1.00m,
            ParaBirimi = "TRY",
            Aciklama = "integration",
            IdempotencyKey = paymentKey
        }, "test", CancellationToken.None);

        var retryRow = await db.PosOdemeIslemleri.FirstAsync(x => x.Id == first.Id);
        retryRow.AgentCommandId = null;
        retryRow.Durum = PosOdemeDurumlari.Unknown;
        await db.SaveChangesAsync();

        var retry = await paymentService.StartAsync(fixture.DeviceId, new PosPaymentBaslatRequest
        {
            PosTerminalId = terminal.Id,
            Tutar = 1.00m,
            ParaBirimi = "TRY",
            Aciklama = "integration",
            PosOdemeIslemiId = first.Id,
            IdempotencyKey = paymentKey
        }, "test", CancellationToken.None);

        Assert.Equal(first.SaleReference, retry.SaleReference);
        var startCommands = await db.Set<STYS.Agent.Entities.AgentCommand>().CountAsync(x => x.AgentId == fixture.MainAgentId && x.CommandType == "PavoStartPayment");
        var resultCommands = await db.Set<STYS.Agent.Entities.AgentCommand>().CountAsync(x => x.AgentId == fixture.MainAgentId && x.CommandType == "PavoGetPaymentResult");
        Assert.Equal(1, startCommands);
        Assert.Equal(1, resultCommands);

        await CleanupAsync(db, suffix);
    }

    [IntegrationFact]
    public async Task Payment_DuplicateStartEngellenir()
    {
        var cs = ConnectionString();
        if (cs is null) return;

        var suffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];
        await using var db = AgentTestSupport.CreateDbContext(cs);
        await db.Database.MigrateAsync();
        var fixture = await SeedAsync(db, suffix);
        var terminalService = CreateTerminalService(db, fixture.KurumId);
        var terminal = await terminalService.KaydetAsync(fixture.DeviceId, null, BuildTerminalRequest(fixture, fixture.MainKrediHesapId, suffix, "PAY-DUP"), CancellationToken.None);
        var paymentService = CreatePaymentService(db, cs, fixture.KurumId);
        var paymentKey = NewPaymentKey();

        var first = await paymentService.StartAsync(fixture.DeviceId, new PosPaymentBaslatRequest
        {
            PosTerminalId = terminal.Id,
            Tutar = 1.00m,
            ParaBirimi = "TRY",
            Aciklama = "integration",
            IdempotencyKey = paymentKey
        }, "test", CancellationToken.None);

        var duplicate = await paymentService.StartAsync(fixture.DeviceId, new PosPaymentBaslatRequest
        {
            PosTerminalId = terminal.Id,
            Tutar = 1.00m,
            ParaBirimi = "TRY",
            Aciklama = "integration",
            PosOdemeIslemiId = first.Id,
            IdempotencyKey = paymentKey
        }, "test", CancellationToken.None);

        Assert.Equal(first.Id, duplicate.Id);
        Assert.Equal(first.SaleReference, duplicate.SaleReference);
        var startCommands = await db.Set<STYS.Agent.Entities.AgentCommand>().CountAsync(x => x.AgentId == fixture.MainAgentId && x.CommandType == "PavoStartPayment");
        Assert.Equal(1, startCommands);

        await CleanupAsync(db, suffix);
    }

    [IntegrationFact]
    public async Task Payment_StartSuccess_ThenGetResultSuccessful()
    {
        var cs = ConnectionString();
        if (cs is null) return;

        var suffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];
        await using var db = AgentTestSupport.CreateDbContext(cs);
        await db.Database.MigrateAsync();
        var fixture = await SeedAsync(db, suffix);
        var terminalService = CreateTerminalService(db, fixture.KurumId);
        var terminal = await terminalService.KaydetAsync(fixture.DeviceId, null, BuildTerminalRequest(fixture, fixture.MainKrediHesapId, suffix, "PAY-OK"), CancellationToken.None);
        var paymentService = CreatePaymentService(db, cs, fixture.KurumId);
        var agentService = CreateAgentCommandService(cs, fixture.KurumId);
        var paymentKey = NewPaymentKey();

        var payment = await paymentService.StartAsync(fixture.DeviceId, new PosPaymentBaslatRequest
        {
            PosTerminalId = terminal.Id,
            Tutar = 1.00m,
            ParaBirimi = "TRY",
            Aciklama = "integration",
            IdempotencyKey = paymentKey
        }, "test", CancellationToken.None);

        await CompletePaymentCommandAsync(db, agentService, fixture.MainAgentId, payment.AgentCommandId!.Value, true, JsonSerializer.Serialize(new PavoStartPaymentResponse
        {
            Data = new PavoPaymentOperationData
            {
                SaleReference = payment.SaleReference,
                IsSuccessful = true,
                IsPending = true,
                ResultCode = "00",
                Message = "accepted",
                TransactionStatus = "PROCESSING"
            }
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        var afterStart = await db.PosOdemeIslemleri.FirstAsync(x => x.Id == payment.Id);
        Assert.Equal(PosOdemeDurumlari.Processing, afterStart.Durum);

        var queried = await paymentService.GetResultAsync(fixture.DeviceId, payment.Id, "test", CancellationToken.None);
        var resultCommand = await db.Set<STYS.Agent.Entities.AgentCommand>().OrderByDescending(x => x.CreatedAt).FirstAsync(x => x.CommandType == "PavoGetPaymentResult");

        await CompletePaymentCommandAsync(db, agentService, fixture.MainAgentId, resultCommand.Id, true, JsonSerializer.Serialize(new PavoGetPaymentResultResponse
        {
            Data = new PavoPaymentOperationData
            {
                SaleReference = payment.SaleReference,
                IsSuccessful = true,
                ResultCode = "00",
                Message = "ok",
                RetrievalReferenceNo = "RRN-1",
                AcquirerReference = "ARC-1",
                AuthorizationCode = "AUTH-1"
            }
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        var afterResult = await db.PosOdemeIslemleri.FirstAsync(x => x.Id == payment.Id);
        Assert.Equal(payment.Id, queried.Id);
        Assert.Equal(PosOdemeDurumlari.Successful, afterResult.Durum);
        Assert.Equal("RRN-1", afterResult.RetrievalReferenceNo);

        var lateStartCommand = await db.Set<STYS.Agent.Entities.AgentCommand>().FirstAsync(x => x.Id == payment.AgentCommandId!.Value);
        lateStartCommand.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();

        var expiryService = CreateCommandExpiryService(cs);
        await expiryService.ExpireTimedOutCommandsAsync(fixture.MainAgentId, CancellationToken.None);

        var leaseToken = (await agentService.GetPendingCommandsAsync(fixture.MainAgentId, CancellationToken.None)).Single().LeaseToken!;
        await agentService.CompleteAsync(payment.AgentCommandId!.Value, fixture.MainAgentId, new AgentCommandCompleteRequest
        {
            Id = payment.AgentCommandId!.Value,
            Success = false,
            LeaseToken = leaseToken,
            ErrorMessage = "late failed start"
        }, CancellationToken.None);

        var afterLateStart = await db.PosOdemeIslemleri.FirstAsync(x => x.Id == payment.Id);
        Assert.Equal(PosOdemeDurumlari.Successful, afterLateStart.Durum);

        var commandsBeforeRepeat = await db.Set<STYS.Agent.Entities.AgentCommand>().CountAsync(x => x.AgentId == fixture.MainAgentId);
        var repeat = await paymentService.StartAsync(fixture.DeviceId, new PosPaymentBaslatRequest
        {
            PosTerminalId = terminal.Id,
            Tutar = 1.00m,
            ParaBirimi = "TRY",
            Aciklama = "integration",
            PosOdemeIslemiId = payment.Id,
            IdempotencyKey = paymentKey
        }, "test", CancellationToken.None);
        var commandsAfterRepeat = await db.Set<STYS.Agent.Entities.AgentCommand>().CountAsync(x => x.AgentId == fixture.MainAgentId);
        Assert.Equal(payment.Id, repeat.Id);
        Assert.Equal(commandsBeforeRepeat, commandsAfterRepeat);

        await CleanupAsync(db, suffix);
    }

    [IntegrationFact]
    public async Task Payment_GetResultFailUpdatesSamePayment()
    {
        var cs = ConnectionString();
        if (cs is null) return;

        var suffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];
        await using var db = AgentTestSupport.CreateDbContext(cs);
        await db.Database.MigrateAsync();
        var fixture = await SeedAsync(db, suffix);
        var terminalService = CreateTerminalService(db, fixture.KurumId);
        var terminal = await terminalService.KaydetAsync(fixture.DeviceId, null, BuildTerminalRequest(fixture, fixture.MainKrediHesapId, suffix, "PAY-FAIL"), CancellationToken.None);
        var paymentService = CreatePaymentService(db, cs, fixture.KurumId);
        var agentService = CreateAgentCommandService(cs, fixture.KurumId);
        var paymentKey = NewPaymentKey();

        var payment = await paymentService.StartAsync(fixture.DeviceId, new PosPaymentBaslatRequest
        {
            PosTerminalId = terminal.Id,
            Tutar = 1.00m,
            ParaBirimi = "TRY",
            Aciklama = "integration",
            IdempotencyKey = paymentKey
        }, "test", CancellationToken.None);

        await CompletePaymentCommandAsync(db, agentService, fixture.MainAgentId, payment.AgentCommandId!.Value, true, JsonSerializer.Serialize(new PavoStartPaymentResponse
        {
            Data = new PavoPaymentOperationData
            {
                SaleReference = payment.SaleReference,
                IsSuccessful = true,
                IsPending = true,
                ResultCode = "00",
                Message = "accepted",
                TransactionStatus = "PROCESSING"
            }
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        await paymentService.GetResultAsync(fixture.DeviceId, payment.Id, "test", CancellationToken.None);
        var resultCommand = await db.Set<STYS.Agent.Entities.AgentCommand>().OrderByDescending(x => x.CreatedAt).FirstAsync(x => x.CommandType == "PavoGetPaymentResult");

        await CompletePaymentCommandAsync(db, agentService, fixture.MainAgentId, resultCommand.Id, false, errorMessage: "rejected");

        var afterResult = await db.PosOdemeIslemleri.FirstAsync(x => x.Id == payment.Id);
        Assert.Equal(PosOdemeDurumlari.Failed, afterResult.Durum);
        Assert.Equal(payment.Id, afterResult.Id);

        var lateStartCommand = await db.Set<STYS.Agent.Entities.AgentCommand>().FirstAsync(x => x.Id == payment.AgentCommandId!.Value);
        lateStartCommand.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();

        var expiryService = CreateCommandExpiryService(cs);
        await expiryService.ExpireTimedOutCommandsAsync(fixture.MainAgentId, CancellationToken.None);

        var leaseToken = (await agentService.GetPendingCommandsAsync(fixture.MainAgentId, CancellationToken.None)).Single().LeaseToken!;
        await agentService.CompleteAsync(payment.AgentCommandId!.Value, fixture.MainAgentId, new AgentCommandCompleteRequest
        {
            Id = payment.AgentCommandId!.Value,
            Success = true,
            LeaseToken = leaseToken,
            ResultPayload = JsonSerializer.Serialize(new PavoStartPaymentResponse
            {
                Data = new PavoPaymentOperationData
                {
                    SaleReference = payment.SaleReference,
                    IsSuccessful = true,
                    ResultCode = "00",
                    Message = "late start ok",
                    TransactionStatus = "PROCESSING"
                }
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web))
        }, CancellationToken.None);

        var afterLateStart = await db.PosOdemeIslemleri.FirstAsync(x => x.Id == payment.Id);
        Assert.Equal(PosOdemeDurumlari.Failed, afterLateStart.Durum);
        Assert.Equal(payment.Id, afterLateStart.Id);

        await CleanupAsync(db, suffix);
    }

    [IntegrationFact]
    public async Task Payment_StartTimeout_UnknownOlur()
    {
        var cs = ConnectionString();
        if (cs is null) return;

        var suffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];
        await using var db = AgentTestSupport.CreateDbContext(cs);
        await db.Database.MigrateAsync();
        var fixture = await SeedAsync(db, suffix);
        var terminalService = CreateTerminalService(db, fixture.KurumId);
        var terminal = await terminalService.KaydetAsync(fixture.DeviceId, null, BuildTerminalRequest(fixture, fixture.MainKrediHesapId, suffix, "PAY-TIMEOUT"), CancellationToken.None);
        var paymentService = CreatePaymentService(db, cs, fixture.KurumId);
        var agentService = CreateAgentCommandService(cs, fixture.KurumId);
        var paymentKey = NewPaymentKey();

        var payment = await paymentService.StartAsync(fixture.DeviceId, new PosPaymentBaslatRequest
        {
            PosTerminalId = terminal.Id,
            Tutar = 1.00m,
            ParaBirimi = "TRY",
            Aciklama = "integration",
            IdempotencyKey = paymentKey
        }, "test", CancellationToken.None);

        var startCommand = await db.Set<STYS.Agent.Entities.AgentCommand>().FirstAsync(x => x.Id == payment.AgentCommandId!.Value);
        startCommand.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();

        var expiryService = CreateCommandExpiryService(cs);
        await expiryService.ExpireTimedOutCommandsAsync(fixture.MainAgentId, CancellationToken.None);

        var after = await db.PosOdemeIslemleri.FirstAsync(x => x.Id == payment.Id);
        Assert.Equal(PosOdemeDurumlari.Unknown, after.Durum);
        Assert.Contains("zaman aşım", after.HataMesaji ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        var expiredCommand = await db.Set<STYS.Agent.Entities.AgentCommand>().FirstAsync(x => x.Id == payment.AgentCommandId!.Value);
        Assert.Equal(STYS.Agent.Contracts.Enums.AgentCommandStatus.Expired, expiredCommand.Status);

        await CleanupAsync(db, suffix);
    }

    [IntegrationFact]
    public async Task Payment_StartTimeout_SonraGetResultIleReconcileEdilir()
    {
        var cs = ConnectionString();
        if (cs is null) return;

        var suffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];
        await using var db = AgentTestSupport.CreateDbContext(cs);
        await db.Database.MigrateAsync();
        var fixture = await SeedAsync(db, suffix);
        var terminalService = CreateTerminalService(db, fixture.KurumId);
        var terminal = await terminalService.KaydetAsync(fixture.DeviceId, null, BuildTerminalRequest(fixture, fixture.MainKrediHesapId, suffix, "PAY-REC"), CancellationToken.None);
        var paymentService = CreatePaymentService(db, cs, fixture.KurumId);
        var agentService = CreateAgentCommandService(cs, fixture.KurumId);
        var paymentKey = NewPaymentKey();

        var payment = await paymentService.StartAsync(fixture.DeviceId, new PosPaymentBaslatRequest
        {
            PosTerminalId = terminal.Id,
            Tutar = 1.00m,
            ParaBirimi = "TRY",
            Aciklama = "integration",
            IdempotencyKey = paymentKey
        }, "test", CancellationToken.None);

        var startCommand = await db.Set<STYS.Agent.Entities.AgentCommand>().FirstAsync(x => x.Id == payment.AgentCommandId!.Value);
        startCommand.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();

        var expiryService = CreateCommandExpiryService(cs);
        await expiryService.ExpireTimedOutCommandsAsync(fixture.MainAgentId, CancellationToken.None);

        var recovery = await paymentService.StartAsync(fixture.DeviceId, new PosPaymentBaslatRequest
        {
            PosTerminalId = terminal.Id,
            Tutar = 1.00m,
            ParaBirimi = "TRY",
            Aciklama = "integration",
            PosOdemeIslemiId = payment.Id,
            IdempotencyKey = paymentKey
        }, "test", CancellationToken.None);

        Assert.Equal(payment.Id, recovery.Id);

        var startCommands = await db.Set<STYS.Agent.Entities.AgentCommand>().CountAsync(x => x.AgentId == fixture.MainAgentId && x.CommandType == "PavoStartPayment");
        var resultCommands = await db.Set<STYS.Agent.Entities.AgentCommand>().CountAsync(x => x.AgentId == fixture.MainAgentId && x.CommandType == "PavoGetPaymentResult");
        Assert.Equal(1, startCommands);
        Assert.Equal(1, resultCommands);

        var resultCommand = await db.Set<STYS.Agent.Entities.AgentCommand>().OrderByDescending(x => x.CreatedAt).FirstAsync(x => x.CommandType == "PavoGetPaymentResult");
        await CompletePaymentCommandAsync(db, agentService, fixture.MainAgentId, resultCommand.Id, true, JsonSerializer.Serialize(new PavoGetPaymentResultResponse
        {
            Data = new PavoPaymentOperationData
            {
                SaleReference = payment.SaleReference,
                IsSuccessful = true,
                ResultCode = "00",
                Message = "ok",
                RetrievalReferenceNo = "RRN-REC",
                AcquirerReference = "ARC-REC",
                AuthorizationCode = "AUTH-REC"
            }
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        var after = await db.PosOdemeIslemleri.FirstAsync(x => x.Id == payment.Id);
        Assert.Equal(PosOdemeDurumlari.Successful, after.Durum);
        Assert.Equal("RRN-REC", after.RetrievalReferenceNo);

        await CleanupAsync(db, suffix);
    }

    [IntegrationFact]
    public async Task Payment_LateExpiredStartPaymentResult_IdempotentUygulanir()
    {
        var cs = ConnectionString();
        if (cs is null) return;

        var suffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];
        await using var db = AgentTestSupport.CreateDbContext(cs);
        await db.Database.MigrateAsync();
        var fixture = await SeedAsync(db, suffix);
        var terminalService = CreateTerminalService(db, fixture.KurumId);
        var terminal = await terminalService.KaydetAsync(fixture.DeviceId, null, BuildTerminalRequest(fixture, fixture.MainKrediHesapId, suffix, "PAY-LATE"), CancellationToken.None);
        var paymentService = CreatePaymentService(db, cs, fixture.KurumId);
        var agentService = CreateAgentCommandService(cs, fixture.KurumId);
        var paymentKey = NewPaymentKey();

        var payment = await paymentService.StartAsync(fixture.DeviceId, new PosPaymentBaslatRequest
        {
            PosTerminalId = terminal.Id,
            Tutar = 1.00m,
            ParaBirimi = "TRY",
            Aciklama = "integration",
            IdempotencyKey = paymentKey
        }, "test", CancellationToken.None);

        var leaseToken = (await agentService.GetPendingCommandsAsync(fixture.MainAgentId, CancellationToken.None)).Single().LeaseToken!;
        await agentService.AcceptAsync(payment.AgentCommandId!.Value, fixture.MainAgentId, CancellationToken.None);
        await agentService.SetRunningAsync(payment.AgentCommandId!.Value, fixture.MainAgentId, CancellationToken.None);

        var startCommand = await db.Set<STYS.Agent.Entities.AgentCommand>().FirstAsync(x => x.Id == payment.AgentCommandId!.Value);
        startCommand.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();

        var expiryService = CreateCommandExpiryService(cs);
        await expiryService.ExpireTimedOutCommandsAsync(fixture.MainAgentId, CancellationToken.None);

        var expired = await db.PosOdemeIslemleri.FirstAsync(x => x.Id == payment.Id);
        Assert.Equal(PosOdemeDurumlari.Unknown, expired.Durum);

        await agentService.CompleteAsync(payment.AgentCommandId!.Value, fixture.MainAgentId, new AgentCommandCompleteRequest
        {
            Id = payment.AgentCommandId!.Value,
            Success = true,
            LeaseToken = leaseToken,
            ResultPayload = JsonSerializer.Serialize(new PavoStartPaymentResponse
            {
                Data = new PavoPaymentOperationData
                {
                    SaleReference = payment.SaleReference,
                    IsSuccessful = true,
                    ResultCode = "00",
                    Message = "accepted",
                    TransactionStatus = "SUCCESS"
                }
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web))
        }, CancellationToken.None);

        var after = await db.PosOdemeIslemleri.FirstAsync(x => x.Id == payment.Id);
        Assert.Equal(PosOdemeDurumlari.Processing, after.Durum);
        var lateStartCommand = await db.Set<STYS.Agent.Entities.AgentCommand>().FirstAsync(x => x.Id == payment.AgentCommandId!.Value);
        Assert.Equal(STYS.Agent.Contracts.Enums.AgentCommandStatus.Expired, lateStartCommand.Status);

        await agentService.CompleteAsync(payment.AgentCommandId!.Value, fixture.MainAgentId, new AgentCommandCompleteRequest
        {
            Id = payment.AgentCommandId!.Value,
            Success = true,
            ResultPayload = JsonSerializer.Serialize(new PavoStartPaymentResponse
            {
                Data = new PavoPaymentOperationData
                {
                    SaleReference = payment.SaleReference,
                    IsSuccessful = true,
                    ResultCode = "00",
                    Message = "accepted",
                    TransactionStatus = "SUCCESS"
                }
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web))
        }, CancellationToken.None);

        var repeated = await db.PosOdemeIslemleri.FirstAsync(x => x.Id == payment.Id);
        Assert.Equal(PosOdemeDurumlari.Processing, repeated.Durum);

        await CleanupAsync(db, suffix);
    }

    [IntegrationFact]
    public async Task Payment_ParallelSequenceValuesDifferent()
    {
        var cs = ConnectionString();
        if (cs is null) return;

        var suffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];
        await using var db = AgentTestSupport.CreateDbContext(cs);
        await db.Database.MigrateAsync();
        var fixture = await SeedAsync(db, suffix);
        var terminalService = CreateTerminalService(db, fixture.KurumId);
        var terminal = await terminalService.KaydetAsync(fixture.DeviceId, null, BuildTerminalRequest(fixture, fixture.MainKrediHesapId, suffix, "PAY-SEQ"), CancellationToken.None);
        var firstKey = NewPaymentKey();
        var secondKey = NewPaymentKey();

        await using var db1 = AgentTestSupport.CreateDbContext(cs);
        await using var db2 = AgentTestSupport.CreateDbContext(cs);
        var paymentService1 = CreatePaymentService(db1, cs, fixture.KurumId);
        var paymentService2 = CreatePaymentService(db2, cs, fixture.KurumId);

        var tasks = new[]
        {
            paymentService1.StartAsync(fixture.DeviceId, new PosPaymentBaslatRequest
            {
                PosTerminalId = terminal.Id,
                Tutar = 1.00m,
                ParaBirimi = "TRY",
                Aciklama = "integration-1",
                IdempotencyKey = firstKey
            }, "test", CancellationToken.None),
            paymentService2.StartAsync(fixture.DeviceId, new PosPaymentBaslatRequest
            {
                PosTerminalId = terminal.Id,
                Tutar = 2.00m,
                ParaBirimi = "TRY",
                Aciklama = "integration-2",
                IdempotencyKey = secondKey
            }, "test", CancellationToken.None)
        };

        await Task.WhenAll(tasks);

        var commands = await db.Set<STYS.Agent.Entities.AgentCommand>()
            .Where(x => x.CommandType == "PavoStartPayment" && x.AgentId == fixture.MainAgentId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync();

        Assert.Equal(2, commands.Count);
        var firstPayload = JsonSerializer.Deserialize<PavoStartPaymentRequest>(commands[0].Payload!, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var secondPayload = JsonSerializer.Deserialize<PavoStartPaymentRequest>(commands[1].Payload!, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotEqual(firstPayload!.TransactionHandle.TransactionSequence, secondPayload!.TransactionHandle.TransactionSequence);

        await CleanupAsync(db, suffix);
    }

    [IntegrationFact]
    public async Task Payment_ParallelStartSameKey_TekCommandOlusturur()
    {
        var cs = ConnectionString();
        if (cs is null) return;

        var suffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];
        await using var db = AgentTestSupport.CreateDbContext(cs);
        await db.Database.MigrateAsync();
        var fixture = await SeedAsync(db, suffix);
        var terminalService = CreateTerminalService(db, fixture.KurumId);
        var terminal = await terminalService.KaydetAsync(fixture.DeviceId, null, BuildTerminalRequest(fixture, fixture.MainKrediHesapId, suffix, "PAY-PAR"), CancellationToken.None);
        var paymentKey = NewPaymentKey();

        await using var db1 = AgentTestSupport.CreateDbContext(cs);
        await using var db2 = AgentTestSupport.CreateDbContext(cs);
        var paymentService1 = CreatePaymentService(db1, cs, fixture.KurumId);
        var paymentService2 = CreatePaymentService(db2, cs, fixture.KurumId);

        var task1 = paymentService1.StartAsync(fixture.DeviceId, new PosPaymentBaslatRequest
        {
            PosTerminalId = terminal.Id,
            Tutar = 1.00m,
            ParaBirimi = "TRY",
            Aciklama = "integration-1",
            IdempotencyKey = paymentKey
        }, "test", CancellationToken.None);
        var task2 = paymentService2.StartAsync(fixture.DeviceId, new PosPaymentBaslatRequest
        {
            PosTerminalId = terminal.Id,
            Tutar = 1.00m,
            ParaBirimi = "TRY",
            Aciklama = "integration-1",
            IdempotencyKey = paymentKey
        }, "test", CancellationToken.None);

        await Task.WhenAll(task1, task2);

        Assert.Equal(task1.Result.Id, task2.Result.Id);
        Assert.Equal(task1.Result.SaleReference, task2.Result.SaleReference);

        var commandCount = await db.Set<STYS.Agent.Entities.AgentCommand>()
            .CountAsync(x => x.AgentId == fixture.MainAgentId && x.CommandType == "PavoStartPayment");
        Assert.Equal(1, commandCount);

        await CleanupAsync(db, suffix);
    }

    [IntegrationFact]
    public async Task Payment_StartPaymentDataNullUnknownOlur()
    {
        var cs = ConnectionString();
        if (cs is null) return;

        var suffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];
        await using var db = AgentTestSupport.CreateDbContext(cs);
        await db.Database.MigrateAsync();
        var fixture = await SeedAsync(db, suffix);
        var terminalService = CreateTerminalService(db, fixture.KurumId);
        var terminal = await terminalService.KaydetAsync(fixture.DeviceId, null, BuildTerminalRequest(fixture, fixture.MainKrediHesapId, suffix, "PAY-NULL"), CancellationToken.None);
        var paymentService = CreatePaymentService(db, cs, fixture.KurumId);
        var agentService = CreateAgentCommandService(cs, fixture.KurumId);
        var paymentKey = NewPaymentKey();

        var payment = await paymentService.StartAsync(fixture.DeviceId, new PosPaymentBaslatRequest
        {
            PosTerminalId = terminal.Id,
            Tutar = 1.00m,
            ParaBirimi = "TRY",
            Aciklama = "integration",
            IdempotencyKey = paymentKey
        }, "test", CancellationToken.None);

        await CompletePaymentCommandAsync(db, agentService, fixture.MainAgentId, payment.AgentCommandId!.Value, true, JsonSerializer.Serialize(new PavoStartPaymentResponse(), new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        var after = await db.PosOdemeIslemleri.FirstAsync(x => x.Id == payment.Id);
        Assert.Equal(PosOdemeDurumlari.Unknown, after.Durum);

        await CleanupAsync(db, suffix);
    }

    [IntegrationFact]
    public async Task Payment_GetPaymentResultUnresolvedUnknownKalir()
    {
        var cs = ConnectionString();
        if (cs is null) return;

        var suffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];
        await using var db = AgentTestSupport.CreateDbContext(cs);
        await db.Database.MigrateAsync();
        var fixture = await SeedAsync(db, suffix);
        var terminalService = CreateTerminalService(db, fixture.KurumId);
        var terminal = await terminalService.KaydetAsync(fixture.DeviceId, null, BuildTerminalRequest(fixture, fixture.MainKrediHesapId, suffix, "PAY-UNKNOWN"), CancellationToken.None);
        var paymentService = CreatePaymentService(db, cs, fixture.KurumId);
        var agentService = CreateAgentCommandService(cs, fixture.KurumId);
        var paymentKey = NewPaymentKey();

        var payment = await paymentService.StartAsync(fixture.DeviceId, new PosPaymentBaslatRequest
        {
            PosTerminalId = terminal.Id,
            Tutar = 1.00m,
            ParaBirimi = "TRY",
            Aciklama = "integration",
            IdempotencyKey = paymentKey
        }, "test", CancellationToken.None);

        await CompletePaymentCommandAsync(db, agentService, fixture.MainAgentId, payment.AgentCommandId!.Value, true, JsonSerializer.Serialize(new PavoStartPaymentResponse
        {
            Data = new PavoPaymentOperationData
            {
                SaleReference = payment.SaleReference,
                IsSuccessful = true,
                IsPending = true,
                ResultCode = "00",
                Message = "accepted",
                TransactionStatus = "PROCESSING"
            }
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        await paymentService.GetResultAsync(fixture.DeviceId, payment.Id, "test", CancellationToken.None);
        var resultCommand = await db.Set<STYS.Agent.Entities.AgentCommand>().OrderByDescending(x => x.CreatedAt).FirstAsync(x => x.CommandType == "PavoGetPaymentResult");
        resultCommand.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();

        var expiryService = CreateCommandExpiryService(cs);
        await expiryService.ExpireTimedOutCommandsAsync(fixture.MainAgentId, CancellationToken.None);

        await CompletePaymentCommandAsync(db, agentService, fixture.MainAgentId, resultCommand.Id, true, JsonSerializer.Serialize(new PavoGetPaymentResultResponse
        {
            Data = new PavoPaymentOperationData
            {
                SaleReference = payment.SaleReference,
                IsUnknown = true,
                ResultCode = "404",
                Message = "not found"
            }
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        var after = await db.PosOdemeIslemleri.FirstAsync(x => x.Id == payment.Id);
        Assert.Equal(PosOdemeDurumlari.Unknown, after.Durum);
        var expiredResultCommand = await db.Set<STYS.Agent.Entities.AgentCommand>().FirstAsync(x => x.Id == resultCommand.Id);
        Assert.Equal(STYS.Agent.Contracts.Enums.AgentCommandStatus.Expired, expiredResultCommand.Status);

        await CleanupAsync(db, suffix);
    }

    [IntegrationFact]
    public async Task Payment_SaleReferenceUniqueConstraintEngellenir()
    {
        var cs = ConnectionString();
        if (cs is null) return;

        var suffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];
        await using var db = AgentTestSupport.CreateDbContext(cs);
        await db.Database.MigrateAsync();
        var fixture = await SeedAsync(db, suffix);
        var terminalService = CreateTerminalService(db, fixture.KurumId);
        var terminal = await terminalService.KaydetAsync(fixture.DeviceId, null, BuildTerminalRequest(fixture, fixture.MainKrediHesapId, suffix, "PAY-UNIQ"), CancellationToken.None);
        var paymentService = CreatePaymentService(db, cs, fixture.KurumId);
        var paymentKey = NewPaymentKey();

        var payment = await paymentService.StartAsync(fixture.DeviceId, new PosPaymentBaslatRequest
        {
            PosTerminalId = terminal.Id,
            Tutar = 1.00m,
            ParaBirimi = "TRY",
            Aciklama = "integration",
            IdempotencyKey = paymentKey
        }, "test", CancellationToken.None);

        var paymentRow = await db.PosOdemeIslemleri.FirstAsync(x => x.Id == payment.Id);

        db.PosOdemeIslemleri.Add(new PosOdemeIslemi
        {
            KurumId = paymentRow.KurumId,
            TesisId = paymentRow.TesisId,
            PosCihaziId = paymentRow.PosCihaziId,
            RezervasyonId = paymentRow.RezervasyonId,
            PosTerminalId = paymentRow.PosTerminalId,
            KasaBankaHesapId = paymentRow.KasaBankaHesapId,
            IdempotencyKey = NewPaymentKey(),
            IslemReferansi = $"{paymentRow.IslemReferansi}-DUP",
            SaleReference = paymentRow.SaleReference,
            SaglayiciIslemId = paymentRow.SaglayiciIslemId,
            Tutar = paymentRow.Tutar,
            ParaBirimi = paymentRow.ParaBirimi,
            Durum = paymentRow.Durum,
            Aciklama = "integration",
            BaslatilmaTarihi = paymentRow.BaslatilmaTarihi,
            AcquirerId = paymentRow.AcquirerId,
            TerminalId = paymentRow.TerminalId,
            MerchantId = paymentRow.MerchantId,
            CreatedBy = "test",
            CreatedAt = DateTime.UtcNow
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        await CleanupAsync(db, suffix);
    }

    private static string? ConnectionString() => Environment.GetEnvironmentVariable("STYS_INTEGRATION_TEST_CONNECTION_STRING");

    private static string NewPaymentKey() => Guid.NewGuid().ToString("N");

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
            new FakeCurrentAgentContext(),
            db,
            CreateAgentCommandService(connectionString, kurumId),
            CreateCommandExpiryService(connectionString));
    }

    private static PosTerminalService CreateTerminalService(StysAppDbContext db, int kurumId) =>
        new(db, [new FakePavoSaglayici()], new FakeKurumTenantAccessor(kurumId));

    private static PosPaymentTestService CreatePaymentService(StysAppDbContext db, string connectionString, int kurumId) =>
        new(db, CreateAgentCommandService(connectionString, kurumId), new FakeKurumTenantAccessor(kurumId));

    private static AgentCommandExpiryService CreateCommandExpiryService(string connectionString) =>
        new(new DbContextFactoryForTest<StysAppDbContext>(() => AgentTestSupport.CreateDbContext(connectionString)), NullLogger<AgentCommandExpiryService>.Instance);

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
            new AgentScope { AgentId = mainAgent.Id, KurumId = kurum.Id, Scope = "agent.command.execute", AktifMi = true, CreatedBy = "test", CreatedAt = DateTime.UtcNow },
            new AgentScope { AgentId = mainAgent.Id, KurumId = kurum.Id, Scope = "agent.command.read", AktifMi = true, CreatedBy = "test", CreatedAt = DateTime.UtcNow },
            new AgentScope { AgentId = mainAgent.Id, KurumId = kurum.Id, Scope = "agent.result.write", AktifMi = true, CreatedBy = "test", CreatedAt = DateTime.UtcNow },
            new AgentScope { AgentId = mainAgent.Id, KurumId = kurum.Id, Scope = "agent.heartbeat", AktifMi = true, CreatedBy = "test", CreatedAt = DateTime.UtcNow });
        db.Set<AgentCapability>().Add(new AgentCapability { AgentId = mainAgent.Id, KurumId = kurum.Id, Capability = "pavo", AktifMi = true, CreatedBy = "test", CreatedAt = DateTime.UtcNow });
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

        var reservation = new Rezervasyon
        {
            ReferansNo = $"RES-{suffix}",
            TesisId = mainTesis.Id,
            KisiSayisi = 1,
            GirisTarihi = DateTime.UtcNow.Date,
            CikisTarihi = DateTime.UtcNow.Date.AddDays(1),
            ToplamBazUcret = 1,
            ToplamUcret = 1,
            ParaBirimi = "TRY",
            MisafirAdiSoyadi = $"Misafir-{suffix}",
            MisafirTelefon = "0000000000",
            RezervasyonDurumu = RezervasyonDurumlari.Onayli,
            AktifMi = true,
            CreatedBy = "test",
            CreatedAt = DateTime.UtcNow
        };
        db.Set<Rezervasyon>().Add(reservation);
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
            reservation.Id,
            device.Id,
            secondDevice.Id,
            device.SeriNo);
    }

    private static void SetProvisionedDeviceHealth(PosCihazi device, string suffix, PavoDeviceHealthStatus status, DateTime? successAt = null, DateTime? checkAt = null, string? error = null)
    {
        device.AktifMi = true;
        device.AgentLocalDeviceId = $"LOCAL-{suffix}";
        device.Fingerprint = $"FP-{suffix}";
        device.TargetFingerprint = $"REMOTE-FP-{suffix}";
        device.EslesmeOnayliMi = true;
        device.LastHealthStatus = status;
        device.LastHealthSuccessAt = successAt;
        device.LastHealthCheckAt = checkAt ?? successAt;
        device.LastHealthError = error;
        device.SonBaglantiTarihi = null;
    }

    private static void SetHealthyDeviceHealth(PosCihazi device, string suffix, DateTime? successAt = null, DateTime? checkAt = null)
    {
        var now = DateTime.UtcNow;
        SetProvisionedDeviceHealth(device, suffix, PavoDeviceHealthStatus.Healthy, successAt ?? now, checkAt ?? now, null);
    }

    private static async Task CleanupAsync(StysAppDbContext db, string suffix)
    {
        var deviceIds = await db.Set<PosCihazi>().IgnoreQueryFilters()
            .Where(x => x.Ad.Contains(suffix) || x.SeriNo.Contains(suffix))
            .Select(x => x.Id)
            .ToListAsync();
        if (deviceIds.Count > 0)
        {
            await db.Set<PosOdemeIslemi>().IgnoreQueryFilters()
                .Where(x => x.PosCihaziId.HasValue && deviceIds.Contains(x.PosCihaziId.Value))
                .ExecuteDeleteAsync();
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
            await db.Set<STYS.Agent.Entities.AgentCommand>().IgnoreQueryFilters().Where(x => agentIds.Contains(x.AgentId)).ExecuteDeleteAsync();
            await db.Set<AgentTesis>().IgnoreQueryFilters().Where(x => agentIds.Contains(x.AgentId)).ExecuteDeleteAsync();
            await db.Set<AgentCredential>().IgnoreQueryFilters().Where(x => agentIds.Contains(x.AgentId)).ExecuteDeleteAsync();
            await db.Set<AgentCapability>().IgnoreQueryFilters().Where(x => agentIds.Contains(x.AgentId)).ExecuteDeleteAsync();
            await db.Set<AgentScope>().IgnoreQueryFilters().Where(x => agentIds.Contains(x.AgentId)).ExecuteDeleteAsync();
            await db.Set<AgentEnrollment>().IgnoreQueryFilters().Where(x => x.AgentId.HasValue && agentIds.Contains(x.AgentId.Value)).ExecuteDeleteAsync();
            await db.Set<AgentEntity>().IgnoreQueryFilters().Where(x => agentIds.Contains(x.Id)).ExecuteDeleteAsync();
        }

        var reservationIds = await db.Set<Rezervasyon>().IgnoreQueryFilters()
            .Where(x => x.ReferansNo.Contains(suffix))
            .Select(x => x.Id)
            .ToListAsync();
        if (reservationIds.Count > 0)
        {
            await db.Set<Rezervasyon>().IgnoreQueryFilters().Where(x => reservationIds.Contains(x.Id)).ExecuteDeleteAsync();
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

    private static async Task CompletePaymentCommandAsync(
        StysAppDbContext db,
        AgentCommandService agentService,
        int agentId,
        Guid commandId,
        bool success,
        string? resultPayload = null,
        string? errorCode = null,
        string? errorMessage = null)
    {
        var leaseToken = (await agentService.GetPendingCommandsAsync(agentId, CancellationToken.None)).Single().LeaseToken!;
        await agentService.AcceptAsync(commandId, agentId, CancellationToken.None);
        await agentService.SetRunningAsync(commandId, agentId, CancellationToken.None);
        await agentService.CompleteAsync(commandId, agentId, new AgentCommandCompleteRequest
        {
            Id = commandId,
            Success = success,
            LeaseToken = leaseToken,
            ResultPayload = resultPayload,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        }, CancellationToken.None);
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
        int ReservationId,
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
