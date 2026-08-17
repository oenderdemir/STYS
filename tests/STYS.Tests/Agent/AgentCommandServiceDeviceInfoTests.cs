using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using STYS.Agent.Contracts.Dtos;
using STYS.Agent.Entities;
using STYS.Agent.Services;
using STYS.Entegrasyonlar.Pos.Entities;
using STYS.Infrastructure.EntityFramework;
using STYS.Tests.TestSupport;
using TOD.Platform.Security.Auth.Services;
using Xunit;

namespace STYS.Tests.Agent;

public sealed class AgentCommandServiceDeviceInfoTests
{
    [Fact]
    public async Task PavoGetDeviceInfo_SameSerialLegacyTerminal_DuplicateOlusturmaz_veMevcutHesabiKorur()
    {
        var dbName = $"agent-device-info-{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<StysAppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        await using var db = new StysAppDbContext(options, new FakeCurrentUserAccessor(), new SuperTenantAccessor());
        var (kurum, _, tesis) = await AgentTestSupport.SeedKurumIlTesisAsync(db, dbName);
        var agent = await AgentTestSupport.SeedAgentAsync(db, kurum.Id, dbName);

        var device = new PosCihazi
        {
            KurumId = kurum.Id,
            TesisId = tesis.Id,
            AgentId = agent.Id,
            Saglayici = PosSaglayici.Pavo,
            Ad = "PAVO POS",
            SeriNo = "PAV200019619",
            IpAdresi = "172.20.10.2",
            HttpPort = 4567,
            EslesmeOnayliMi = true,
            AktifMi = true,
            CreatedBy = "test",
            CreatedAt = DateTime.UtcNow
        };
        db.PosCihazlari.Add(device);
        await db.SaveChangesAsync();

        var legacyTerminal = new PosTerminal
        {
            KurumId = kurum.Id,
            TesisId = tesis.Id,
            PosCihaziId = device.Id,
            KasaBankaHesapId = 12345,
            SaglayiciKodu = "PAVO",
            Ad = "Legacy Terminal",
            SerialNumber = "02811543",
            SourceTerminalReference = "000000000985629",
            SourceFingerprint = null,
            CanonicalAcquirerId = string.Empty,
            CanonicalTerminalId = string.Empty,
            AktifMi = true,
            CreatedBy = "test",
            CreatedAt = DateTime.UtcNow
        };
        db.PosTerminaller.Add(legacyTerminal);
        await db.SaveChangesAsync();

        var service = new AgentCommandService(
            new DbContextFactoryForTest<StysAppDbContext>(() => new StysAppDbContext(options, new FakeCurrentUserAccessor(), new SuperTenantAccessor())),
            new FakeSuperAdminTenantAccessor(),
            NullLogger<AgentCommandService>.Instance);

        var response = new PavoGetDeviceInfoResponse
        {
            Terminals =
            [
                new PavoDeviceTerminalInfo
                {
                    AcquirerId = "10",
                    AcquirerName = "T.C.ZİRAAT BANKASI A.Ş.",
                    TerminalId = "02811543",
                    MerchantId = "000000000985629"
                }
            ]
        };

        var syncMethod = typeof(AgentCommandService).GetMethod("SyncDiscoveredTerminals", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(syncMethod);
        syncMethod!.Invoke(service, [db, device, response.Terminals]);
        await db.SaveChangesAsync();

        var terminals = await db.PosTerminaller.AsNoTracking()
            .Where(x => x.PosCihaziId == device.Id && !x.IsDeleted)
            .ToListAsync();

        Assert.Single(terminals);
        var updated = terminals.Single();
        Assert.Equal(12345, updated.KasaBankaHesapId);
        Assert.Equal("10", updated.AcquirerId);
        Assert.Equal("T.C.ZİRAAT BANKASI A.Ş.", updated.AcquirerName);
        Assert.Equal("02811543", updated.SerialNumber);
        Assert.Equal("02811543", updated.CanonicalTerminalId);
        Assert.Equal("10", updated.CanonicalAcquirerId);
        Assert.Equal("000000000985629", updated.SourceTerminalReference);
    }

    private sealed class FakeCurrentUserAccessor : ICurrentUserAccessor
    {
        public string? GetCurrentUserName() => "test";
        public Guid? GetCurrentUserId() => Guid.NewGuid();
        public string? GetCurrentUserEmail() => "test@example.com";
    }
}
