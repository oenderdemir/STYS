using Microsoft.EntityFrameworkCore;
using STYS.Agent.Contracts.Enums;
using STYS.Agent.Entities;
using AgentEntity = STYS.Agent.Entities.Agent;
using STYS.Infrastructure.EntityFramework;
using STYS.Kurumlar.Entities;
using STYS.Iller.Entities;
using STYS.Tesisler.Entities;

namespace STYS.Tests.TestSupport;

public static class AgentTestSupport
{
    public static StysAppDbContext CreateDbContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<StysAppDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new StysAppDbContext(options);
    }

    public static async Task<(Kurum kurum, Il il, Tesis tesis)> SeedKurumIlTesisAsync(
        StysAppDbContext db, string uniqueSuffix, CancellationToken ct = default)
    {
        var il = new Il { Ad = $"Il-{uniqueSuffix}", AktifMi = true };
        db.Set<Il>().Add(il);
        await db.SaveChangesAsync(ct);

        var kurum = new Kurum { Kod = $"KRM-{uniqueSuffix}", Ad = $"Kurum-{uniqueSuffix}", AktifMi = true };
        db.Set<Kurum>().Add(kurum);
        await db.SaveChangesAsync(ct);

        var tesis = new Tesis
        {
            Ad = $"Tesis-{uniqueSuffix}",
            KurumId = kurum.Id,
            IlId = il.Id,
            Telefon = "000",
            Adres = "Adres",
            AktifMi = true
        };
        db.Set<Tesis>().Add(tesis);
        await db.SaveChangesAsync(ct);

        return (kurum, il, tesis);
    }

    public static async Task<AgentEntity> SeedAgentAsync(StysAppDbContext db, int kurumId, string uniqueSuffix, CancellationToken ct = default)
    {
        var agent = new AgentEntity
        {
            Ad = $"Agent-{uniqueSuffix}",
            AgentKey = $"AGNT-{uniqueSuffix}"[..16],
            KurumId = kurumId,
            Durum = AgentDurum.Active,
            CreatedBy = "test",
            CreatedAt = DateTime.UtcNow
        };
        db.Set<AgentEntity>().Add(agent);
        await db.SaveChangesAsync(ct);
        return agent;
    }

    public static async Task<(AgentCredential credential, string rawSecret)> SeedCredentialAsync(
        StysAppDbContext db, int agentId, int kurumId, CancellationToken ct = default)
    {
        var raw = "test-secret-" + Guid.NewGuid().ToString("N")[..8];
        var hash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(raw)));

        var cred = new AgentCredential
        {
            AgentId = agentId,
            KurumId = kurumId,
            ClientId = $"client-{Guid.NewGuid():N}"[..20],
            ClientSecretHash = hash,
            AktifMi = true,
            CreatedBy = "test",
            CreatedAt = DateTime.UtcNow
        };
        db.Set<AgentCredential>().Add(cred);
        await db.SaveChangesAsync(ct);
        return (cred, raw);
    }

    public static async Task CleanupAsync(StysAppDbContext db, string uniqueSuffix, CancellationToken ct = default)
    {
        var agentEntries = await db.Set<AgentEntity>().Where(x => x.AgentKey.StartsWith($"AGNT-{uniqueSuffix}")).ToListAsync(ct);
        foreach (var entry in agentEntries)
        {
            var creds = await db.Set<AgentCredential>().Where(x => x.AgentId == entry.Id).ToListAsync(ct);
            db.Set<AgentCredential>().RemoveRange(creds);
            var tesisLinks = await db.Set<AgentTesis>().Where(x => x.AgentId == entry.Id).ToListAsync(ct);
            db.Set<AgentTesis>().RemoveRange(tesisLinks);
            var enrollments = await db.Set<AgentEnrollment>().Where(x => x.AgentId == entry.Id).ToListAsync(ct);
            db.Set<AgentEnrollment>().RemoveRange(enrollments);
        }
        db.Set<AgentEntity>().RemoveRange(agentEntries);
        await db.SaveChangesAsync(ct);
    }
}
