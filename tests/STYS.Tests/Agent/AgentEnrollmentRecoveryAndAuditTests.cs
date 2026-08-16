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

    // ---------------------------------------------------------------- A2. parallel recovery

    [IntegrationFact]
    public async Task ParalelRecovery_TekBirAktifCredentialBirakir()
    {
        var db = await SetupAsync(); if (db is null) return;
        var code = await NewCodeAsync();
        const string nonce = "recovery-nonce-parallel-cccccccccccccccc";
        var instanceId = Guid.NewGuid().ToString("N");
        var agentKey = $"AGNT-{_suffix}";

        var lost = await NewTokenService().EnrollAsync(new AgentEnrollmentRequest
        {
            EnrollmentCode = code.Code!,
            AgentKey = agentKey,
            CihazKimligi = instanceId,
            RegistrationNonce = nonce
        }, CancellationToken.None);

        // Two recovery retries race with identical code + nonce + machine identity.
        int success = 0, rejected = 0, unexpected = 0;
        var results = new AgentEnrollmentResponse?[2];
        var tasks = new Task[2];
        for (var i = 0; i < 2; i++)
        {
            var idx = i;
            tasks[i] = Task.Run(async () =>
            {
                try
                {
                    results[idx] = await NewTokenService().EnrollAsync(new AgentEnrollmentRequest
                    {
                        EnrollmentCode = code.Code!,
                        AgentKey = agentKey,
                        CihazKimligi = instanceId,
                        RegistrationNonce = nonce
                    }, CancellationToken.None);
                    Interlocked.Increment(ref success);
                }
                catch (TOD.Platform.SharedKernel.Exceptions.BaseException)
                {
                    Interlocked.Increment(ref rejected);
                }
                catch (DbUpdateConcurrencyException)
                {
                    Interlocked.Increment(ref rejected);
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref unexpected);
                    Console.WriteLine($"Unexpected: {ex.GetType().Name}: {ex.Message}");
                }
            });
        }
        await Task.WhenAll(tasks);

        Assert.Equal(0, unexpected);
        Assert.Equal(1, success);
        Assert.Equal(1, rejected);

        await using var verify = AgentTestSupport.CreateDbContext(_cs);

        // Still exactly one Agent.
        Assert.Equal(1, await verify.Set<AgentEntity>().CountAsync(x => x.AgentKey == agentKey && !x.IsDeleted));

        // And exactly one active credential: the orphan plus the losing attempt must not survive.
        var active = await verify.Set<AgentCredential>().AsNoTracking()
            .Where(x => x.AgentId == lost.AgentId && x.AktifMi && !x.IsDeleted)
            .ToListAsync();
        Assert.Single(active);

        // The surviving credential is the one the winning caller was handed, and it works.
        var winner = results.Single(x => x is not null)!;
        Assert.Equal(winner.ClientId, active[0].ClientId);
        var token = await NewTokenService().IssueTokenAsync(new AgentTokenRequest
        {
            ClientId = winner.ClientId,
            ClientSecret = winner.ClientSecret,
            AgentInstanceId = instanceId
        }, CancellationToken.None);
        Assert.NotNull(token);

        // The original orphan is revoked.
        var orphan = await verify.Set<AgentCredential>().AsNoTracking().FirstAsync(x => x.ClientId == lost.ClientId);
        Assert.False(orphan.AktifMi);

        await AgentTestSupport.CleanupAsync(verify, _suffix);
    }

    // ---------------------------------------------------------------- B2. single-use invariant

    [IntegrationFact]
    public async Task EnrollmentCode_MaxKullanimSayisiBirdenBuyukOlamaz()
    {
        var db = await SetupAsync(); if (db is null) return;

        var svc = new AgentService(NewFactory(), new FakeKurumTenantAccessor(_kurumId));
        var code = await svc.GenerateEnrollmentCodeAsync(new AgentEnrollmentCodeRequest
        {
            TesisIds = [_tesisId],
            AllowedScopes = ["agent.heartbeat"],
#pragma warning disable CS0618 // deliberately exercising the legacy property
            MaxKullanimSayisi = 5,
#pragma warning restore CS0618
            ExpirationHours = 1
        }, "test", CancellationToken.None);

        // Server normalizes the caller's request: the code is single-use.
        Assert.Equal(1, code.MaxKullanimSayisi);
        await using var stored = AgentTestSupport.CreateDbContext(_cs);
        Assert.Equal(1, (await stored.Set<AgentEnrollment>().AsNoTracking().FirstAsync(x => x.Id == code.Id)).MaxKullanimSayisi);

        // First registration consumes it...
        await NewTokenService().EnrollAsync(new AgentEnrollmentRequest
        {
            EnrollmentCode = code.Code!,
            AgentKey = $"AGNT-{_suffix}-1",
            CihazKimligi = Guid.NewGuid().ToString("N"),
            RegistrationNonce = "nonce-one-dddddddddddddddddddddddddd"
        }, CancellationToken.None);

        // ...and a second, genuinely different installation is refused.
        var ex = await Assert.ThrowsAsync<TOD.Platform.SharedKernel.Exceptions.BaseException>(
            () => NewTokenService().EnrollAsync(new AgentEnrollmentRequest
            {
                EnrollmentCode = code.Code!,
                AgentKey = $"AGNT-{_suffix}-2",
                CihazKimligi = Guid.NewGuid().ToString("N"),
                RegistrationNonce = "nonce-two-eeeeeeeeeeeeeeeeeeeeeeeeee"
            }, CancellationToken.None));
        Assert.Equal(PublicInvalidMessage, ex.Message);

        await using var verify = AgentTestSupport.CreateDbContext(_cs);
        Assert.Equal(0, await verify.Set<AgentEntity>().CountAsync(x => x.AgentKey == $"AGNT-{_suffix}-2" && !x.IsDeleted));
        await AgentTestSupport.CleanupAsync(verify, _suffix);
    }

    // ---------------------------------------------------------------- C/D. recovery state guards

    [IntegrationFact]
    public async Task Recovery_OrijinalCihazKimligiYoksaFailClosed()
    {
        var db = await SetupAsync(); if (db is null) return;
        var code = await NewCodeAsync();
        const string nonce = "recovery-nonce-nomachine-ffffffffffffffff";

        // Original registration recorded no machine identity, so there is nothing to bind to.
        await NewTokenService().EnrollAsync(new AgentEnrollmentRequest
        {
            EnrollmentCode = code.Code!,
            AgentKey = $"AGNT-{_suffix}",
            CihazKimligi = null,
            RegistrationNonce = nonce
        }, CancellationToken.None);

        // Even holding the correct nonce, recovery is refused rather than accepted on nonce alone.
        var ex = await Assert.ThrowsAsync<TOD.Platform.SharedKernel.Exceptions.BaseException>(
            () => NewTokenService().EnrollAsync(new AgentEnrollmentRequest
            {
                EnrollmentCode = code.Code!,
                AgentKey = $"AGNT-{_suffix}",
                CihazKimligi = Guid.NewGuid().ToString("N"),
                RegistrationNonce = nonce
            }, CancellationToken.None));
        Assert.Equal(PublicInvalidMessage, ex.Message);

        await using var verify = AgentTestSupport.CreateDbContext(_cs);
        await AgentTestSupport.CleanupAsync(verify, _suffix);
    }

    [IntegrationFact]
    public async Task Recovery_RevokeVeyaExpiredEnrollmentIcinCalismaz()
    {
        var db = await SetupAsync(); if (db is null) return;
        var svc = new AgentService(NewFactory(), new FakeKurumTenantAccessor(_kurumId));

        // --- revoked after a successful registration ---
        var revokedCode = await NewCodeAsync();
        const string revokedNonce = "recovery-nonce-revoked-gggggggggggggggg";
        var revokedInstance = Guid.NewGuid().ToString("N");
        await NewTokenService().EnrollAsync(new AgentEnrollmentRequest
        {
            EnrollmentCode = revokedCode.Code!,
            AgentKey = $"AGNT-{_suffix}-rev",
            CihazKimligi = revokedInstance,
            RegistrationNonce = revokedNonce
        }, CancellationToken.None);
        await svc.RevokeEnrollmentCodeAsync(revokedCode.Id, CancellationToken.None);

        var revokedEx = await Assert.ThrowsAsync<TOD.Platform.SharedKernel.Exceptions.BaseException>(
            () => NewTokenService().EnrollAsync(new AgentEnrollmentRequest
            {
                EnrollmentCode = revokedCode.Code!,
                AgentKey = $"AGNT-{_suffix}-rev",
                CihazKimligi = revokedInstance,
                RegistrationNonce = revokedNonce
            }, CancellationToken.None));
        Assert.Equal(PublicInvalidMessage, revokedEx.Message);

        // --- expired after a successful registration ---
        var expiredCode = await NewCodeAsync();
        const string expiredNonce = "recovery-nonce-expired-hhhhhhhhhhhhhhhh";
        var expiredInstance = Guid.NewGuid().ToString("N");
        await NewTokenService().EnrollAsync(new AgentEnrollmentRequest
        {
            EnrollmentCode = expiredCode.Code!,
            AgentKey = $"AGNT-{_suffix}-exp",
            CihazKimligi = expiredInstance,
            RegistrationNonce = expiredNonce
        }, CancellationToken.None);

        await using (var mutate = AgentTestSupport.CreateDbContext(_cs))
        {
            var row = await mutate.Set<AgentEnrollment>().FirstAsync(x => x.Id == expiredCode.Id);
            row.ExpiresAt = DateTime.UtcNow.AddHours(-1);
            await mutate.SaveChangesAsync();
        }

        var expiredEx = await Assert.ThrowsAsync<TOD.Platform.SharedKernel.Exceptions.BaseException>(
            () => NewTokenService().EnrollAsync(new AgentEnrollmentRequest
            {
                EnrollmentCode = expiredCode.Code!,
                AgentKey = $"AGNT-{_suffix}-exp",
                CihazKimligi = expiredInstance,
                RegistrationNonce = expiredNonce
            }, CancellationToken.None));
        Assert.Equal(PublicInvalidMessage, expiredEx.Message);

        // No extra credentials were minted by either refused recovery.
        await using var verify = AgentTestSupport.CreateDbContext(_cs);
        foreach (var key in new[] { $"AGNT-{_suffix}-rev", $"AGNT-{_suffix}-exp" })
        {
            var agent = await verify.Set<AgentEntity>().AsNoTracking().FirstAsync(x => x.AgentKey == key && !x.IsDeleted);
            Assert.Equal(1, await verify.Set<AgentCredential>().CountAsync(x => x.AgentId == agent.Id && x.AktifMi && !x.IsDeleted));
        }

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
    public async Task KurumPolitikasiKapali_PolicyEndpointFalseDonerVeAgentAktifOlur()
    {
        var db = await SetupAsync(kurumRequiresApproval: false); if (db is null) return;

        // The read-only endpoint must reflect what is actually stored, not a hardcoded default.
        var svc = new AgentService(NewFactory(), new FakeKurumTenantAccessor(_kurumId));
        var policy = await svc.GetEnrollmentPolicyAsync(CancellationToken.None);
        Assert.Equal(_kurumId, policy.KurumId);
        Assert.False(policy.RequiresApproval);

        // Kurum false + code false is the only combination that activates immediately.
        var code = await NewCodeAsync(requiresApproval: false);
        var result = await NewTokenService().EnrollAsync(new AgentEnrollmentRequest
        {
            EnrollmentCode = code.Code!,
            AgentKey = $"AGNT-{_suffix}",
            CihazKimligi = Guid.NewGuid().ToString("N")
        }, CancellationToken.None);

        Assert.Equal((int)AgentDurum.Active, result.Durum);

        // And an Active agent can immediately obtain a token.
        var token = await NewTokenService().IssueTokenAsync(new AgentTokenRequest
        {
            ClientId = result.ClientId,
            ClientSecret = result.ClientSecret,
            AgentInstanceId = "x"
        }, CancellationToken.None);
        Assert.NotNull(token);

        await using var verify = AgentTestSupport.CreateDbContext(_cs);
        await AgentTestSupport.CleanupAsync(verify, _suffix);
    }

    [IntegrationFact]
    public async Task KurumZorunluOnay_PolicyEndpointTrueDoner()
    {
        var db = await SetupAsync(kurumRequiresApproval: true); if (db is null) return;

        var svc = new AgentService(NewFactory(), new FakeKurumTenantAccessor(_kurumId));
        var policy = await svc.GetEnrollmentPolicyAsync(CancellationToken.None);

        Assert.True(policy.RequiresApproval);

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
