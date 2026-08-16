using Microsoft.EntityFrameworkCore;
using STYS.Agent.Contracts.Dtos;
using STYS.Agent.Contracts.Enums;
using STYS.Agent.Entities;
using STYS.Agent.Services;
using STYS.Infrastructure.EntityFramework;
using STYS.Tests.TestSupport;
using Xunit;
using AgentEntity = STYS.Agent.Entities.Agent;

namespace STYS.Tests.Agent;

/// <summary>
/// E2D4.1 server-side closure: registration-response-loss recovery, kurum approval policy
/// precedence, approve/reject audit, and the unified public error contract.
/// </summary>
[Collection(SqlServerIntegrationCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Domain", "Agent")]
[Trait("TestLevel", "SqlIntegration")]
public sealed class AgentEnrollmentRecoveryAndAuditTests : IAsyncLifetime
{
    private const string TestMarker = "e2d41";
    private const string PublicInvalidMessage = "Enrollment kodu geçersiz veya kullanılamaz durumda.";

    private string _suffix = string.Empty;
    private string _cs = string.Empty;
    private int _kurumId;
    private int _tesisId;

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<StysAppDbContext?> SetupAsync(bool kurumRequiresApproval = false)
    {
        var cs = Environment.GetEnvironmentVariable(IntegrationFactAttribute.ConnectionStringEnvVar);
        if (string.IsNullOrWhiteSpace(cs)) return null;
        _cs = cs;
        _suffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];

        var db = AgentTestSupport.CreateDbContext(cs);
        var (kurum, _, tesis) = await AgentTestSupport.SeedKurumIlTesisAsync(db, _suffix);
        _kurumId = kurum.Id;
        _tesisId = tesis.Id;

        kurum.AgentEnrollmentRequiresApproval = kurumRequiresApproval;
        await db.SaveChangesAsync();
        return db;
    }

    private DbContextFactoryForTest<StysAppDbContext> NewFactory() => new(() => AgentTestSupport.CreateDbContext(_cs));

    private AgentTokenService NewTokenService() => new(NewFactory(), CreateJwtService());

    private static AgentJwtTokenService CreateJwtService() =>
        new(Microsoft.Extensions.Options.Options.Create(
            new TOD.Platform.Security.Auth.Options.JwtTokenOptions
            {
                Key = "01234567890123456789012345678901!!!",
                AccessTokenExpirationMinutes = 60
            }));

    private async Task<AgentEnrollmentCodeDto> NewCodeAsync(bool requiresApproval = false)
    {
        var svc = new AgentService(NewFactory(), new FakeKurumTenantAccessor(_kurumId));
        return await svc.GenerateEnrollmentCodeAsync(new AgentEnrollmentCodeRequest
        {
            TesisIds = [_tesisId],
            AllowedScopes = ["agent.heartbeat"],
            MaxKullanimSayisi = 1,
            ExpirationHours = 1,
            RequiresApproval = requiresApproval
        }, "test", CancellationToken.None);
    }

    // ---------------------------------------------------------------- A. response-loss recovery

    [IntegrationFact]
    public async Task ResponseLoss_AyniInstallationRetry_TekAgentVeKullanilabilirYeniCredential()
    {
        var db = await SetupAsync(); if (db is null) return;
        var code = await NewCodeAsync();
        const string nonce = "recovery-nonce-aaaaaaaaaaaaaaaaaaaaaaaa";
        var instanceId = Guid.NewGuid().ToString("N");
        var agentKey = $"AGNT-{_suffix}";

        // First registration commits server-side; pretend the response never reached the agent.
        var lost = await NewTokenService().EnrollAsync(new AgentEnrollmentRequest
        {
            EnrollmentCode = code.Code!,
            AgentKey = agentKey,
            CihazKimligi = instanceId,
            RegistrationNonce = nonce
        }, CancellationToken.None);

        // Same installation retries with the identical nonce and instance identity.
        var recovered = await NewTokenService().EnrollAsync(new AgentEnrollmentRequest
        {
            EnrollmentCode = code.Code!,
            AgentKey = agentKey,
            CihazKimligi = instanceId,
            RegistrationNonce = nonce
        }, CancellationToken.None);

        // Same Agent, not a second one.
        Assert.Equal(lost.AgentId, recovered.AgentId);
        await using var verify = AgentTestSupport.CreateDbContext(_cs);
        Assert.Equal(1, await verify.Set<AgentEntity>().CountAsync(x => x.AgentKey == agentKey && !x.IsDeleted));

        // The recovered credential is new and usable...
        Assert.NotEqual(lost.ClientId, recovered.ClientId);
        var token = await NewTokenService().IssueTokenAsync(new AgentTokenRequest
        {
            ClientId = recovered.ClientId,
            ClientSecret = recovered.ClientSecret,
            AgentInstanceId = instanceId
        }, CancellationToken.None);
        Assert.NotNull(token);

        // ...and the orphaned one the agent never received is revoked.
        var orphan = await verify.Set<AgentCredential>().AsNoTracking()
            .FirstAsync(x => x.ClientId == lost.ClientId);
        Assert.False(orphan.AktifMi);
        Assert.NotNull(orphan.RevokedAt);

        await AgentTestSupport.CleanupAsync(verify, _suffix);
    }

    // ---------------------------------------------------------------- B. recovery cannot be stolen

    [IntegrationFact]
    public async Task ResponseLoss_FarkliInstallationRecoveryYapamaz()
    {
        var db = await SetupAsync(); if (db is null) return;
        var code = await NewCodeAsync();
        const string nonce = "recovery-nonce-bbbbbbbbbbbbbbbbbbbbbbbb";
        var instanceId = Guid.NewGuid().ToString("N");

        await NewTokenService().EnrollAsync(new AgentEnrollmentRequest
        {
            EnrollmentCode = code.Code!,
            AgentKey = $"AGNT-{_suffix}",
            CihazKimligi = instanceId,
            RegistrationNonce = nonce
        }, CancellationToken.None);

        // Attacker holds the (spent) code but not the nonce.
        var withoutNonce = await Assert.ThrowsAsync<TOD.Platform.SharedKernel.Exceptions.BaseException>(
            () => NewTokenService().EnrollAsync(new AgentEnrollmentRequest
            {
                EnrollmentCode = code.Code!,
                AgentKey = $"OTHER-{_suffix}",
                CihazKimligi = Guid.NewGuid().ToString("N")
            }, CancellationToken.None));
        Assert.Equal(PublicInvalidMessage, withoutNonce.Message);

        // Attacker guesses a different nonce.
        var wrongNonce = await Assert.ThrowsAsync<TOD.Platform.SharedKernel.Exceptions.BaseException>(
            () => NewTokenService().EnrollAsync(new AgentEnrollmentRequest
            {
                EnrollmentCode = code.Code!,
                AgentKey = $"OTHER-{_suffix}",
                CihazKimligi = Guid.NewGuid().ToString("N"),
                RegistrationNonce = "not-the-right-nonce-zzzzzzzzzzzzzzzz"
            }, CancellationToken.None));
        Assert.Equal(PublicInvalidMessage, wrongNonce.Message);

        // Even WITH the nonce, a different machine identity is refused.
        var wrongMachine = await Assert.ThrowsAsync<TOD.Platform.SharedKernel.Exceptions.BaseException>(
            () => NewTokenService().EnrollAsync(new AgentEnrollmentRequest
            {
                EnrollmentCode = code.Code!,
                AgentKey = $"OTHER-{_suffix}",
                CihazKimligi = Guid.NewGuid().ToString("N"),
                RegistrationNonce = nonce
            }, CancellationToken.None));
        Assert.Equal(PublicInvalidMessage, wrongMachine.Message);

        await using var verify = AgentTestSupport.CreateDbContext(_cs);
        Assert.Equal(0, await verify.Set<AgentEntity>().CountAsync(x => x.AgentKey == $"OTHER-{_suffix}" && !x.IsDeleted));
        await AgentTestSupport.CleanupAsync(verify, _suffix);
    }

    // ---------------------------------------------------------------- C/D. kurum approval policy

    [IntegrationFact]
    public async Task KurumZorunluOnay_RequestFalseOlsaBilePendingApproval()
    {
        var db = await SetupAsync(kurumRequiresApproval: true); if (db is null) return;
        // Enrollment code explicitly asks for NO approval; kurum policy must win.
        var code = await NewCodeAsync(requiresApproval: false);

        var result = await NewTokenService().EnrollAsync(new AgentEnrollmentRequest
        {
            EnrollmentCode = code.Code!,
            AgentKey = $"AGNT-{_suffix}",
            CihazKimligi = Guid.NewGuid().ToString("N")
        }, CancellationToken.None);

        Assert.Equal((int)AgentDurum.PendingApproval, result.Durum);

        // And it genuinely cannot get a token.
        var denied = await Assert.ThrowsAsync<TOD.Platform.SharedKernel.Exceptions.BaseException>(
            () => NewTokenService().IssueTokenAsync(new AgentTokenRequest
            {
                ClientId = result.ClientId,
                ClientSecret = result.ClientSecret,
                AgentInstanceId = "x"
            }, CancellationToken.None));
        Assert.Equal(403, denied.ErrorCode);

        await using var verify = AgentTestSupport.CreateDbContext(_cs);
        await AgentTestSupport.CleanupAsync(verify, _suffix);
    }

    [IntegrationFact]
    public async Task KurumPolitikasiKapali_RequestTrueIsePendingApproval()
    {
        var db = await SetupAsync(kurumRequiresApproval: false); if (db is null) return;
        var code = await NewCodeAsync(requiresApproval: true);

        var result = await NewTokenService().EnrollAsync(new AgentEnrollmentRequest
        {
            EnrollmentCode = code.Code!,
            AgentKey = $"AGNT-{_suffix}",
            CihazKimligi = Guid.NewGuid().ToString("N")
        }, CancellationToken.None);

        Assert.Equal((int)AgentDurum.PendingApproval, result.Durum);

        await using var verify = AgentTestSupport.CreateDbContext(_cs);
        await AgentTestSupport.CleanupAsync(verify, _suffix);
    }

    // ---------------------------------------------------------------- E/F. approve / reject audit

    [IntegrationFact]
    public async Task Approve_AuditAlanlariniDoldurur()
    {
        var db = await SetupAsync(kurumRequiresApproval: true); if (db is null) return;
        var code = await NewCodeAsync();
        var result = await NewTokenService().EnrollAsync(new AgentEnrollmentRequest
        {
            EnrollmentCode = code.Code!,
            AgentKey = $"AGNT-{_suffix}",
            CihazKimligi = Guid.NewGuid().ToString("N")
        }, CancellationToken.None);

        var svc = new AgentService(NewFactory(), new FakeKurumTenantAccessor(_kurumId));
        await svc.ApproveAsync(result.AgentId, "operator@stys", CancellationToken.None);

        await using var verify = AgentTestSupport.CreateDbContext(_cs);
        var agent = await verify.Set<AgentEntity>().AsNoTracking().FirstAsync(x => x.Id == result.AgentId);

        Assert.Equal(AgentDurum.Active, agent.Durum);
        Assert.NotNull(agent.ApprovedAt);
        Assert.Equal("operator@stys", agent.ApprovedBy);
        Assert.Null(agent.RejectedAt);
        Assert.Null(agent.RejectedBy);

        await AgentTestSupport.CleanupAsync(verify, _suffix);
    }

    [IntegrationFact]
    public async Task Reject_AuditAlanlariniDoldururVeCredentialIptalEder()
    {
        var db = await SetupAsync(kurumRequiresApproval: true); if (db is null) return;
        var code = await NewCodeAsync();
        var result = await NewTokenService().EnrollAsync(new AgentEnrollmentRequest
        {
            EnrollmentCode = code.Code!,
            AgentKey = $"AGNT-{_suffix}",
            CihazKimligi = Guid.NewGuid().ToString("N")
        }, CancellationToken.None);

        var svc = new AgentService(NewFactory(), new FakeKurumTenantAccessor(_kurumId));
        await svc.RejectAsync(result.AgentId, "operator@stys", CancellationToken.None);

        await using var verify = AgentTestSupport.CreateDbContext(_cs);
        var agent = await verify.Set<AgentEntity>().AsNoTracking().FirstAsync(x => x.Id == result.AgentId);

        Assert.Equal(AgentDurum.Rejected, agent.Durum);
        Assert.NotNull(agent.RejectedAt);
        Assert.Equal("operator@stys", agent.RejectedBy);

        var credential = await verify.Set<AgentCredential>().AsNoTracking().FirstAsync(x => x.ClientId == result.ClientId);
        Assert.False(credential.AktifMi);
        Assert.NotNull(credential.RevokedAt);

        // A rejected agent can never trade its credential for a token.
        var denied = await Assert.ThrowsAsync<TOD.Platform.SharedKernel.Exceptions.BaseException>(
            () => NewTokenService().IssueTokenAsync(new AgentTokenRequest
            {
                ClientId = result.ClientId,
                ClientSecret = result.ClientSecret,
                AgentInstanceId = "x"
            }, CancellationToken.None));
        Assert.Equal(403, denied.ErrorCode);

        await AgentTestSupport.CleanupAsync(verify, _suffix);
    }

    // ---------------------------------------------------------------- G. unified public error

    [IntegrationFact]
    public async Task GecersizEnrollmentDurumlari_AyniGenericHatayiDondurur()
    {
        var db = await SetupAsync(); if (db is null) return;
        var messages = new List<string>();

        async Task<string> CaptureAsync(string enrollmentCode)
        {
            var ex = await Assert.ThrowsAsync<TOD.Platform.SharedKernel.Exceptions.BaseException>(
                () => NewTokenService().EnrollAsync(new AgentEnrollmentRequest
                {
                    EnrollmentCode = enrollmentCode,
                    AgentKey = $"AGNT-{_suffix}-{Guid.NewGuid():N}"[..24],
                    CihazKimligi = Guid.NewGuid().ToString("N")
                }, CancellationToken.None));
            return ex.Message;
        }

        // Unknown code.
        messages.Add(await CaptureAsync("TOTALLYUNKNOWNCODE"));

        // Used code (consumed by a successful registration).
        var usedCode = await NewCodeAsync();
        await NewTokenService().EnrollAsync(new AgentEnrollmentRequest
        {
            EnrollmentCode = usedCode.Code!,
            AgentKey = $"AGNT-{_suffix}-used",
            CihazKimligi = Guid.NewGuid().ToString("N")
        }, CancellationToken.None);
        messages.Add(await CaptureAsync(usedCode.Code!));

        // Expired code.
        var expiredCode = await NewCodeAsync();
        await using (var mutate = AgentTestSupport.CreateDbContext(_cs))
        {
            var row = await mutate.Set<AgentEnrollment>().FirstAsync(x => x.Id == expiredCode.Id);
            row.ExpiresAt = DateTime.UtcNow.AddHours(-1);
            await mutate.SaveChangesAsync();
        }
        messages.Add(await CaptureAsync(expiredCode.Code!));

        // Revoked code.
        var revokedCode = await NewCodeAsync();
        var svc = new AgentService(NewFactory(), new FakeKurumTenantAccessor(_kurumId));
        await svc.RevokeEnrollmentCodeAsync(revokedCode.Id, CancellationToken.None);
        messages.Add(await CaptureAsync(revokedCode.Code!));

        // Every rejection is byte-identical, so probing reveals nothing about which codes exist.
        Assert.All(messages, m => Assert.Equal(PublicInvalidMessage, m));
        Assert.Single(messages.Distinct());

        await using var verify = AgentTestSupport.CreateDbContext(_cs);
        await AgentTestSupport.CleanupAsync(verify, _suffix);
    }
}
