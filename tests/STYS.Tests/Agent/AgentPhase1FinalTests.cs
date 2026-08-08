using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using STYS.Agent.Authorization;
using STYS.Agent.Contracts.Enums;
using STYS.Agent.Entities;
using STYS.Agent.Services;
using STYS.Infrastructure.EntityFramework;
using STYS.Tests.TestSupport;
using Xunit;
using AgentEntity = STYS.Agent.Entities.Agent;

namespace STYS.Tests.Agent;

[Collection(SqlServerIntegrationCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Domain", "Agent")]
[Trait("TestLevel", "SqlIntegration")]
public sealed class AgentPhase1FinalTests : IAsyncLifetime
{
    private const string TestMarker = "ph1fin";
    private string _uniqueSuffix = string.Empty;
    private string _cs = string.Empty;
    private int _kurumAId;
    private int _tesisAId;

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<StysAppDbContext> SetupAsync()
    {
        var cs = Environment.GetEnvironmentVariable(IntegrationFactAttribute.ConnectionStringEnvVar);
        if (string.IsNullOrWhiteSpace(cs)) return null!;
        _cs = cs;
        _uniqueSuffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];
        var db = AgentTestSupport.CreateDbContext(cs);
        var (ka, _, ta) = await AgentTestSupport.SeedKurumIlTesisAsync(db, _uniqueSuffix);
        _kurumAId = ka.Id; _tesisAId = ta.Id;
        return db;
    }

    private DbContextFactoryForTest<StysAppDbContext> NewFactory() => new(() => AgentTestSupport.CreateDbContext(_cs));

    // ===== 1. CONCURRENCY (strict) =====

    [IntegrationFact]
    public async Task Enrollment_2Parallel_Strict_Exactly1Success1Reject()
    {
        var db = await SetupAsync(); if (db is null) return;
        var factory = NewFactory();
        var svc = new AgentService(factory, new FakeSuperAdminTenantAccessor());

        var codeReq = new STYS.Agent.Contracts.Dtos.AgentEnrollmentCodeRequest { KurumId = _kurumAId, TesisIds = [_tesisAId], AllowedScopes = ["agent.heartbeat"], MaxKullanimSayisi = 1, ExpirationHours = 1 };
        var enrollment = await svc.GenerateEnrollmentCodeAsync(codeReq, "test", CancellationToken.None);

        int success = 0, expectedFail = 0, unexpectedFail = 0;
        var tasks = new Task[2];
        for (int i = 0; i < 2; i++)
        {
            var idx = i;
            tasks[i] = Task.Run(async () =>
            {
                try
                {
                    var connFactory = new DbContextFactoryForTest<StysAppDbContext>(() => AgentTestSupport.CreateDbContext(_cs));
                    var ts = new AgentTokenService(connFactory, CreateJwtService());
                    await ts.EnrollAsync(new STYS.Agent.Contracts.Dtos.AgentEnrollmentRequest { EnrollmentCode = enrollment.Code, AgentKey = $"AGNT-{_uniqueSuffix}-{idx}" }, CancellationToken.None);
                    Interlocked.Increment(ref success);
                }
                catch (TOD.Platform.SharedKernel.Exceptions.BaseException)
                {
                    Interlocked.Increment(ref expectedFail);
                }
                catch (DbUpdateConcurrencyException)
                {
                    Interlocked.Increment(ref expectedFail);
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref unexpectedFail);
                    Console.WriteLine($"Unexpected: {ex.GetType().Name}: {ex.Message}");
                }
            });
        }
        await Task.WhenAll(tasks);

        Assert.Equal(1, success);
        Assert.Equal(1, expectedFail);
        Assert.Equal(0, unexpectedFail);

        var verify = AgentTestSupport.CreateDbContext(_cs);
        Assert.Equal(1, await verify.Set<AgentEntity>().CountAsync(x => x.AgentKey.Contains(_uniqueSuffix)));
        var agent = await verify.Set<AgentEntity>().FirstAsync(x => x.AgentKey.Contains(_uniqueSuffix));
        Assert.Equal(1, await verify.Set<AgentCredential>().CountAsync(x => x.AgentId == agent.Id));
        Assert.Equal(1, await verify.Set<AgentScope>().CountAsync(x => x.AgentId == agent.Id));
        Assert.Equal(1, await verify.Set<AgentTesis>().CountAsync(x => x.AgentId == agent.Id));

        var enr = await verify.Set<AgentEnrollment>().FirstAsync(x => x.Code == enrollment.Code);
        Assert.Equal(1, enr.KullanimSayisi);
        Assert.Equal(AgentEnrollmentDurum.Used, enr.Durum);
        Assert.Equal(agent.Id, enr.AgentId);

        await AgentTestSupport.CleanupAsync(verify, _uniqueSuffix);
    }

    [IntegrationFact]
    public async Task Enrollment_3Parallel_Strict_Exactly1Success2Reject()
    {
        var db = await SetupAsync(); if (db is null) return;
        var factory = NewFactory();
        var svc = new AgentService(factory, new FakeSuperAdminTenantAccessor());

        var codeReq = new STYS.Agent.Contracts.Dtos.AgentEnrollmentCodeRequest { KurumId = _kurumAId, TesisIds = [_tesisAId], AllowedScopes = ["agent.heartbeat"], MaxKullanimSayisi = 1, ExpirationHours = 1 };
        var enrollment = await svc.GenerateEnrollmentCodeAsync(codeReq, "test", CancellationToken.None);

        int success = 0, expectedFail = 0, unexpectedFail = 0;
        var tasks = new Task[3];
        for (int i = 0; i < 3; i++)
        {
            var idx = i;
            tasks[i] = Task.Run(async () =>
            {
                try
                {
                    var connFactory = new DbContextFactoryForTest<StysAppDbContext>(() => AgentTestSupport.CreateDbContext(_cs));
                    var ts = new AgentTokenService(connFactory, CreateJwtService());
                    await ts.EnrollAsync(new STYS.Agent.Contracts.Dtos.AgentEnrollmentRequest { EnrollmentCode = enrollment.Code, AgentKey = $"AGNT-{_uniqueSuffix}-{idx}" }, CancellationToken.None);
                    Interlocked.Increment(ref success);
                }
                catch (TOD.Platform.SharedKernel.Exceptions.BaseException)
                {
                    Interlocked.Increment(ref expectedFail);
                }
                catch (DbUpdateConcurrencyException)
                {
                    Interlocked.Increment(ref expectedFail);
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref unexpectedFail);
                    Console.WriteLine($"Unexpected: {ex.GetType().Name}: {ex.Message}");
                }
            });
        }
        await Task.WhenAll(tasks);

        Assert.Equal(1, success);
        Assert.Equal(2, expectedFail);
        Assert.Equal(0, unexpectedFail);

        var verify = AgentTestSupport.CreateDbContext(_cs);
        Assert.Equal(1, await verify.Set<AgentEntity>().CountAsync(x => x.AgentKey.Contains(_uniqueSuffix)));
        var agent = await verify.Set<AgentEntity>().FirstAsync(x => x.AgentKey.Contains(_uniqueSuffix));
        Assert.Equal(1, await verify.Set<AgentCredential>().CountAsync(x => x.AgentId == agent.Id));

        await AgentTestSupport.CleanupAsync(verify, _uniqueSuffix);
    }

    // ===== 2. TRANSACTION ROLLBACK =====

    [IntegrationFact]
    public async Task Enrollment_Rollback_NoOrphanRecords()
    {
        var db = await SetupAsync(); if (db is null) return;
        var factory = NewFactory();
        var svc = new AgentService(factory, new FakeSuperAdminTenantAccessor());

        var codeReq = new STYS.Agent.Contracts.Dtos.AgentEnrollmentCodeRequest { KurumId = _kurumAId, TesisIds = [_tesisAId], AllowedScopes = ["agent.heartbeat"], MaxKullanimSayisi = 1, ExpirationHours = 1 };
        var enrollment = await svc.GenerateEnrollmentCodeAsync(codeReq, "test", CancellationToken.None);

        var throwingHook = new ThrowingEnrollmentHook();
        var ts = new AgentTokenService(new DbContextFactoryForTest<StysAppDbContext>(() => AgentTestSupport.CreateDbContext(_cs)), CreateJwtService(), throwingHook);

        var ex = await Assert.ThrowsAnyAsync<Exception>(() => ts.EnrollAsync(new STYS.Agent.Contracts.Dtos.AgentEnrollmentRequest { EnrollmentCode = enrollment.Code, AgentKey = $"ROLLBACK-{_uniqueSuffix}" }, CancellationToken.None));

        var verify = AgentTestSupport.CreateDbContext(_cs);
        Assert.Equal(0, await verify.Set<AgentEntity>().CountAsync(x => x.AgentKey.Contains(_uniqueSuffix)));
        Assert.Equal(0, await verify.Set<AgentCredential>().CountAsync(x => verify.Set<AgentEntity>().Any(a => a.AgentKey.Contains(_uniqueSuffix) && a.Id == x.AgentId)));
        Assert.Equal(0, await verify.Set<AgentScope>().CountAsync(x => verify.Set<AgentEntity>().Any(a => a.AgentKey.Contains(_uniqueSuffix) && a.Id == x.AgentId)));
        Assert.Equal(0, await verify.Set<AgentTesis>().CountAsync(x => verify.Set<AgentEntity>().Any(a => a.AgentKey.Contains(_uniqueSuffix) && a.Id == x.AgentId)));

        var enr = await verify.Set<AgentEnrollment>().FirstAsync(x => x.Code == enrollment.Code);
        Assert.Equal(0, enr.KullanimSayisi);
        Assert.Equal(AgentEnrollmentDurum.Active, enr.Durum);
        Assert.Null(enr.AgentId);

        await AgentTestSupport.CleanupAsync(verify, _uniqueSuffix);
    }

    // ===== 3. OLD JWT INVALIDATION VIA CREDENTIAL VERSION =====

    [IntegrationFact]
    public async Task ScopeChange_OldJwt_RejectedByCredentialVersionHandler()
    {
        var db = await SetupAsync(); if (db is null) return;
        var factory = NewFactory();
        var svc = new AgentService(factory, new FakeSuperAdminTenantAccessor());

        var agent = await AgentTestSupport.SeedAgentAsync(db, _kurumAId, _uniqueSuffix);
        var (cred, rawSecret) = await AgentTestSupport.SeedCredentialAsync(db, agent.Id, _kurumAId);
        await svc.UpdateScopesAsync(agent.Id, new List<string> { "agent.heartbeat", "agent.config.read" }, CancellationToken.None);

        var tokenService = new AgentTokenService(factory, CreateJwtService());
        var token = await tokenService.IssueTokenAsync(new STYS.Agent.Contracts.Dtos.AgentTokenRequest { ClientId = cred.ClientId, ClientSecret = rawSecret, AgentInstanceId = "test" }, CancellationToken.None);
        var jwtClaims = ParseJwt(token.AccessToken);

        // Verify old JWT passes handler
        var handler = new AgentCredentialValidationHandler(factory);
        var context = CreateAuthContext(jwtClaims);
        await handler.HandleAsync(context);
        Assert.True(context.HasSucceeded);

        // Change scope → CredentialVersion++
        await svc.UpdateScopesAsync(agent.Id, new List<string> { "agent.heartbeat" }, CancellationToken.None);
        await db.Entry(cred).ReloadAsync();

        // Old JWT should now be rejected (credentialVersion mismatch)
        var context2 = CreateAuthContext(jwtClaims);
        await handler.HandleAsync(context2);
        Assert.False(context2.HasSucceeded);

        await AgentTestSupport.CleanupAsync(db, _uniqueSuffix);
    }

    [IntegrationFact]
    public async Task DisableAgent_OldJwt_RejectedByHandler()
    {
        var db = await SetupAsync(); if (db is null) return;
        var factory = NewFactory();
        var svc = new AgentService(factory, new FakeSuperAdminTenantAccessor());

        var agent = await AgentTestSupport.SeedAgentAsync(db, _kurumAId, _uniqueSuffix);
        var (cred, rawSecret) = await AgentTestSupport.SeedCredentialAsync(db, agent.Id, _kurumAId);

        var tokenService = new AgentTokenService(factory, CreateJwtService());
        var token = await tokenService.IssueTokenAsync(new STYS.Agent.Contracts.Dtos.AgentTokenRequest { ClientId = cred.ClientId, ClientSecret = rawSecret, AgentInstanceId = "test" }, CancellationToken.None);
        var jwtClaims = ParseJwt(token.AccessToken);

        var handler = new AgentCredentialValidationHandler(factory);
        var ctx = CreateAuthContext(jwtClaims);
        await handler.HandleAsync(ctx);
        Assert.True(ctx.HasSucceeded);

        await svc.DisableAsync(agent.Id, CancellationToken.None);

        var ctx2 = CreateAuthContext(jwtClaims);
        await handler.HandleAsync(ctx2);
        Assert.False(ctx2.HasSucceeded);

        await AgentTestSupport.CleanupAsync(db, _uniqueSuffix);
    }

    [IntegrationFact]
    public async Task RevokeAgent_OldJwt_RejectedByHandler()
    {
        var db = await SetupAsync(); if (db is null) return;
        var factory = NewFactory();
        var svc = new AgentService(factory, new FakeSuperAdminTenantAccessor());

        var agent = await AgentTestSupport.SeedAgentAsync(db, _kurumAId, _uniqueSuffix);
        var (cred, rawSecret) = await AgentTestSupport.SeedCredentialAsync(db, agent.Id, _kurumAId);

        var tokenService = new AgentTokenService(factory, CreateJwtService());
        var token = await tokenService.IssueTokenAsync(new STYS.Agent.Contracts.Dtos.AgentTokenRequest { ClientId = cred.ClientId, ClientSecret = rawSecret, AgentInstanceId = "test" }, CancellationToken.None);
        var jwtClaims = ParseJwt(token.AccessToken);

        var handler = new AgentCredentialValidationHandler(factory);
        var ctx = CreateAuthContext(jwtClaims);
        await handler.HandleAsync(ctx);
        Assert.True(ctx.HasSucceeded);

        await svc.RevokeAsync(agent.Id, CancellationToken.None);

        var ctx2 = CreateAuthContext(jwtClaims);
        await handler.HandleAsync(ctx2);
        Assert.False(ctx2.HasSucceeded);

        await AgentTestSupport.CleanupAsync(db, _uniqueSuffix);
    }

    [IntegrationFact]
    public async Task NewJwtAfterScopeChange_HasCorrectScopes()
    {
        var db = await SetupAsync(); if (db is null) return;
        var factory = NewFactory();
        var svc = new AgentService(factory, new FakeSuperAdminTenantAccessor());

        var agent = await AgentTestSupport.SeedAgentAsync(db, _kurumAId, _uniqueSuffix);
        var (cred, rawSecret) = await AgentTestSupport.SeedCredentialAsync(db, agent.Id, _kurumAId);
        await svc.UpdateScopesAsync(agent.Id, new List<string> { "agent.heartbeat", "stys.pavo.payment.execute" }, CancellationToken.None);

        var tokenService = new AgentTokenService(factory, CreateJwtService());
        var oldToken = await tokenService.IssueTokenAsync(new STYS.Agent.Contracts.Dtos.AgentTokenRequest { ClientId = cred.ClientId, ClientSecret = rawSecret, AgentInstanceId = "test" }, CancellationToken.None);
        Assert.Contains("stys.pavo.payment.execute", ParseJwt(oldToken.AccessToken).First(c => c.Type == "agentScopes").Value);

        await svc.UpdateScopesAsync(agent.Id, new List<string> { "agent.heartbeat" }, CancellationToken.None);

        var newToken = await tokenService.IssueTokenAsync(new STYS.Agent.Contracts.Dtos.AgentTokenRequest { ClientId = cred.ClientId, ClientSecret = rawSecret, AgentInstanceId = "test" }, CancellationToken.None);
        Assert.DoesNotContain("stys.pavo.payment.execute", ParseJwt(newToken.AccessToken).First(c => c.Type == "agentScopes").Value);
        Assert.Contains("agent.heartbeat", ParseJwt(newToken.AccessToken).First(c => c.Type == "agentScopes").Value);

        await AgentTestSupport.CleanupAsync(db, _uniqueSuffix);
    }

    // ===== HELPERS =====

    private List<Claim> ParseJwt(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        return jwt.Claims.ToList();
    }

    private static AuthorizationHandlerContext CreateAuthContext(List<Claim> claims)
    {
        var identity = new ClaimsIdentity(claims, "AgentScheme");
        var principal = new ClaimsPrincipal(identity);
        return new AuthorizationHandlerContext(new[] { new AgentCredentialRequirement() }, principal, null);
    }

    private static AgentJwtTokenService CreateJwtService() =>
        new(Microsoft.Extensions.Options.Options.Create(new TOD.Platform.Security.Auth.Options.JwtTokenOptions { Key = "01234567890123456789012345678901!!!", AccessTokenExpirationMinutes = 60 }));

    private sealed class ThrowingEnrollmentHook : IAgentEnrollmentExecutionHook
    {
        public Task AfterEntitiesCreatedBeforeCommitAsync(AgentEntity agent, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Test-controlled rollback failure.");
    }
}
