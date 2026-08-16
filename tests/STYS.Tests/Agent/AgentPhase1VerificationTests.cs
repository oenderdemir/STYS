using System.IdentityModel.Tokens.Jwt;
using Microsoft.EntityFrameworkCore;
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
[Trait("CriticalInvariant", "TenantIsolation")]
public sealed class AgentPhase1VerificationTests : IAsyncLifetime
{
    private const string TestMarker = "ph1vrf";
    private string _uniqueSuffix = string.Empty;
    private int _kurumAId;
    private int _kurumBId;
    private int _tesisAId;
    private int _tesisBId;

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<StysAppDbContext> SetupAsync()
    {
        var cs = Environment.GetEnvironmentVariable(IntegrationFactAttribute.ConnectionStringEnvVar);
        if (string.IsNullOrWhiteSpace(cs)) return null!;
        _cs = cs;
        _uniqueSuffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];
        var db = AgentTestSupport.CreateDbContext(cs);
        var (ka, _, ta) = await AgentTestSupport.SeedKurumIlTesisAsync(db, $"{_uniqueSuffix}-A");
        var (kb, _, tb) = await AgentTestSupport.SeedKurumIlTesisAsync(db, $"{_uniqueSuffix}-B");
        _kurumAId = ka.Id; _tesisAId = ta.Id;
        _kurumBId = kb.Id; _tesisBId = tb.Id;
        return db;
    }
    private string _cs = string.Empty;

    private DbContextFactoryForTest<StysAppDbContext> NewFactory() => new(() => AgentTestSupport.CreateDbContext(_cs));

    // ===== 1. SCOPE UPDATE TESTS (FIXED) =====

    [IntegrationFact]
    public async Task Scope_AddScope_TokenContainsScopeInJwtClaim()
    {
        var db = await SetupAsync(); if (db is null) return;
        var factory = NewFactory();
        var service = new AgentService(factory, new FakeKurumTenantAccessor(_kurumAId));

        var agent = await AgentTestSupport.SeedAgentAsync(db, _kurumAId, _uniqueSuffix);
        var (cred, rawSecret) = await AgentTestSupport.SeedCredentialAsync(db, agent.Id, _kurumAId);

        await service.UpdateScopesAsync(agent.Id, new List<string> { "agent.heartbeat", "stys.pavo.payment.execute" }, CancellationToken.None);

        var tokenService = CreateTokenService(factory);
        var token = await tokenService.IssueTokenAsync(new STYS.Agent.Contracts.Dtos.AgentTokenRequest { ClientId = cred.ClientId, ClientSecret = rawSecret, AgentInstanceId = "test" }, CancellationToken.None);

        var scopesClaim = ParseJwtClaim(token.AccessToken, "agentScopes");
        Assert.Contains("agent.heartbeat", scopesClaim);
        Assert.Contains("stys.pavo.payment.execute", scopesClaim);

        await AgentTestSupport.CleanupAsync(db, _uniqueSuffix);
    }

    [IntegrationFact]
    public async Task Scope_RemoveScope_NewTokenDoesNotContainScope()
    {
        var db = await SetupAsync(); if (db is null) return;
        var factory = NewFactory();
        var service = new AgentService(factory, new FakeKurumTenantAccessor(_kurumAId));

        var agent = await AgentTestSupport.SeedAgentAsync(db, _kurumAId, _uniqueSuffix);
        var (cred, rawSecret) = await AgentTestSupport.SeedCredentialAsync(db, agent.Id, _kurumAId);
        await service.UpdateScopesAsync(agent.Id, new List<string> { "agent.heartbeat", "stys.pavo.payment.execute" }, CancellationToken.None);

        var token1 = await CreateTokenService(factory).IssueTokenAsync(new STYS.Agent.Contracts.Dtos.AgentTokenRequest { ClientId = cred.ClientId, ClientSecret = rawSecret, AgentInstanceId = "test" }, CancellationToken.None);
        Assert.Contains("stys.pavo.payment.execute", ParseJwtClaim(token1.AccessToken, "agentScopes"));

        await service.UpdateScopesAsync(agent.Id, new List<string> { "agent.heartbeat" }, CancellationToken.None);

        var token2 = await CreateTokenService(factory).IssueTokenAsync(new STYS.Agent.Contracts.Dtos.AgentTokenRequest { ClientId = cred.ClientId, ClientSecret = rawSecret, AgentInstanceId = "test" }, CancellationToken.None);
        Assert.DoesNotContain("stys.pavo.payment.execute", ParseJwtClaim(token2.AccessToken, "agentScopes"));
        Assert.Contains("agent.heartbeat", ParseJwtClaim(token2.AccessToken, "agentScopes"));

        var activeScopes = await db.Set<AgentScope>().Where(x => x.AgentId == agent.Id && !x.IsDeleted && x.AktifMi).Select(x => x.Scope).ToListAsync();
        Assert.Single(activeScopes);
        Assert.Equal("agent.heartbeat", activeScopes[0]);

        var totalCount = await db.Set<AgentScope>().IgnoreQueryFilters().CountAsync(x => x.AgentId == agent.Id && x.IsDeleted);
        Assert.Equal(1, totalCount);

        await AgentTestSupport.CleanupAsync(db, _uniqueSuffix);
    }

    [IntegrationFact]
    public async Task Scope_ChangeInvalidatesCredentialVersion()
    {
        var db = await SetupAsync(); if (db is null) return;
        var factory = NewFactory();
        var service = new AgentService(factory, new FakeKurumTenantAccessor(_kurumAId));

        var agent = await AgentTestSupport.SeedAgentAsync(db, _kurumAId, _uniqueSuffix);
        var (cred, rawSecret) = await AgentTestSupport.SeedCredentialAsync(db, agent.Id, _kurumAId);
        var originalVersion = cred.CredentialVersion;

        await service.UpdateScopesAsync(agent.Id, new List<string> { "agent.heartbeat", "agent.config.read" }, CancellationToken.None);

        await db.Entry(cred).ReloadAsync();
        Assert.True(cred.CredentialVersion > originalVersion);
        await AgentTestSupport.CleanupAsync(db, _uniqueSuffix);
    }

    [IntegrationFact]
    public async Task Scope_CaseInsensitiveNormalization()
    {
        var db = await SetupAsync(); if (db is null) return;
        var factory = NewFactory();
        var service = new AgentService(factory, new FakeKurumTenantAccessor(_kurumAId));

        var agent = await AgentTestSupport.SeedAgentAsync(db, _kurumAId, _uniqueSuffix);
        await service.UpdateScopesAsync(agent.Id, new List<string> { "AGENT.HEARTBEAT", "Agent.Config.Read" }, CancellationToken.None);

        var scopes = await db.Set<AgentScope>().Where(x => x.AgentId == agent.Id && !x.IsDeleted && x.AktifMi).Select(x => x.Scope).ToListAsync();
        Assert.Contains("agent.heartbeat", scopes);
        Assert.Contains("agent.config.read", scopes);
        await AgentTestSupport.CleanupAsync(db, _uniqueSuffix);
    }

    [IntegrationFact]
    public async Task Scope_DuplicateScope_Prevented()
    {
        var db = await SetupAsync(); if (db is null) return;
        var factory = NewFactory();
        var service = new AgentService(factory, new FakeKurumTenantAccessor(_kurumAId));

        var agent = await AgentTestSupport.SeedAgentAsync(db, _kurumAId, _uniqueSuffix);
        await service.UpdateScopesAsync(agent.Id, new List<string> { "agent.heartbeat" }, CancellationToken.None);
        await service.UpdateScopesAsync(agent.Id, new List<string> { "agent.heartbeat", "agent.heartbeat" }, CancellationToken.None);

        var count = await db.Set<AgentScope>().CountAsync(x => x.AgentId == agent.Id && x.Scope == "agent.heartbeat" && !x.IsDeleted && x.AktifMi);
        Assert.Equal(1, count);
        await AgentTestSupport.CleanupAsync(db, _uniqueSuffix);
    }

    // ===== 2. ENROLLMENT CONCURRENCY (moved to AgentPhase1FinalTests) =====

    // [IntegrationFact]
    public async Task Enrollment_Concurrent_2Calls_CreatesOneAgent()
    {
        var cs = Environment.GetEnvironmentVariable(IntegrationFactAttribute.ConnectionStringEnvVar);
        if (string.IsNullOrWhiteSpace(cs)) return;
        _uniqueSuffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];

        var dbSetup = AgentTestSupport.CreateDbContext(cs);
        var (kurum, _, tesis) = await AgentTestSupport.SeedKurumIlTesisAsync(dbSetup, _uniqueSuffix);
        _kurumAId = kurum.Id; _tesisAId = tesis.Id;

        var factory = NewFactory();
        var service = new AgentService(new DbContextFactoryForTest<StysAppDbContext>(() => AgentTestSupport.CreateDbContext(cs)), new FakeKurumTenantAccessor(_kurumAId));
        var codeReq = new STYS.Agent.Contracts.Dtos.AgentEnrollmentCodeRequest { TesisIds = [_tesisAId], AllowedScopes = ["agent.heartbeat"], MaxKullanimSayisi = 1, ExpirationHours = 1 };
        var enrollment = await service.GenerateEnrollmentCodeAsync(codeReq, "test", CancellationToken.None);

        int success = 0, expectedFail = 0, unexpectedFail = 0;
        var tasks = new Task[2];
        for (int i = 0; i < 2; i++)
        {
            var idx = i;
            tasks[i] = Task.Run(async () =>
            {
                try
                {
                    var connDb = AgentTestSupport.CreateDbContext(cs);
                    var connFactory = new DbContextFactoryForTest<StysAppDbContext>(() => AgentTestSupport.CreateDbContext(cs));
                    var ts = new AgentTokenService(connFactory, CreateJwtService());
                    await ts.EnrollAsync(new STYS.Agent.Contracts.Dtos.AgentEnrollmentRequest { EnrollmentCode = enrollment.Code, AgentKey = $"AGNT-{_uniqueSuffix}-{idx}" }, CancellationToken.None);
                    Interlocked.Increment(ref success);
                }
                catch (TOD.Platform.SharedKernel.Exceptions.BaseException)
                {
                    Interlocked.Increment(ref expectedFail);
                }
                catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
                {
                    Interlocked.Increment(ref expectedFail);
                }
                catch
                {
                    Interlocked.Increment(ref unexpectedFail);
                }
            });
        }

        await Task.WhenAll(tasks);

        Assert.True(success <= 1);
        Assert.Equal(0, unexpectedFail);

        var verifyDb = AgentTestSupport.CreateDbContext(cs);
        var agentCount = await verifyDb.Set<AgentEntity>().CountAsync(x => x.AgentKey.Contains(_uniqueSuffix));
        var scopesCount = await verifyDb.Set<AgentScope>().CountAsync(x => verifyDb.Set<AgentEntity>().Where(a => a.AgentKey.Contains(_uniqueSuffix)).Select(a => a.Id).Contains(x.AgentId));
        Assert.True(agentCount <= 1);
        Assert.True(scopesCount <= 1);

        var updatedEnrollment = await verifyDb.Set<AgentEnrollment>().FirstAsync(x => x.Id == enrollment.Id);
        Assert.True(updatedEnrollment.KullanimSayisi <= 1);

        await AgentTestSupport.CleanupAsync(verifyDb, _uniqueSuffix);
    }

    // ===== 3. REQUIRES APPROVAL =====

    [IntegrationFact]
    public async Task RequiresApproval_Pending_CannotGetToken()
    {
        var db = await SetupAsync(); if (db is null) return;
        var factory = NewFactory();
        var service = new AgentService(factory, new FakeKurumTenantAccessor(_kurumAId));

        var codeReq = new STYS.Agent.Contracts.Dtos.AgentEnrollmentCodeRequest { TesisIds = [_tesisAId], AllowedScopes = ["agent.heartbeat"], MaxKullanimSayisi = 1, RequiresApproval = true };
        var enrollment = await service.GenerateEnrollmentCodeAsync(codeReq, "test", CancellationToken.None);

        var tokenService = CreateTokenService(factory);
        var result = await tokenService.EnrollAsync(new STYS.Agent.Contracts.Dtos.AgentEnrollmentRequest { EnrollmentCode = enrollment.Code, AgentKey = $"PENDING-{_uniqueSuffix}" }, CancellationToken.None);
        Assert.Equal((int)AgentDurum.PendingApproval, result.Durum);

        var ex = await Assert.ThrowsAsync<TOD.Platform.SharedKernel.Exceptions.BaseException>(() =>
            tokenService.IssueTokenAsync(new STYS.Agent.Contracts.Dtos.AgentTokenRequest { ClientId = result.ClientId, ClientSecret = result.ClientSecret, AgentInstanceId = "test" }, CancellationToken.None));
        Assert.Equal(403, ex.ErrorCode);

        await service.ApproveAsync(result.AgentId, CancellationToken.None);
        var token = await tokenService.IssueTokenAsync(new STYS.Agent.Contracts.Dtos.AgentTokenRequest { ClientId = result.ClientId, ClientSecret = result.ClientSecret, AgentInstanceId = "test" }, CancellationToken.None);
        Assert.NotNull(token);

        await AgentTestSupport.CleanupAsync(db, _uniqueSuffix);
    }

    [IntegrationFact]
    public async Task RequiresApproval_False_ActiveImmediately()
    {
        var db = await SetupAsync(); if (db is null) return;
        var factory = NewFactory();
        var service = new AgentService(factory, new FakeKurumTenantAccessor(_kurumAId));

        var codeReq = new STYS.Agent.Contracts.Dtos.AgentEnrollmentCodeRequest { TesisIds = [_tesisAId], AllowedScopes = ["agent.heartbeat"], RequiresApproval = false };
        var enrollment = await service.GenerateEnrollmentCodeAsync(codeReq, "test", CancellationToken.None);
        var tokenService = CreateTokenService(factory);
        var result = await tokenService.EnrollAsync(new STYS.Agent.Contracts.Dtos.AgentEnrollmentRequest { EnrollmentCode = enrollment.Code, AgentKey = $"ACTIVE-{_uniqueSuffix}" }, CancellationToken.None);
        Assert.Equal((int)AgentDurum.Active, result.Durum);

        await AgentTestSupport.CleanupAsync(db, _uniqueSuffix);
    }

    // ===== 4. KURUM ISOLATION =====

    [IntegrationFact]
    public async Task KurumA_CannotAccessKurumB_AllOperations()
    {
        var db = await SetupAsync(); if (db is null) return;
        var factory = NewFactory();
        var agentA = await AgentTestSupport.SeedAgentAsync(db, _kurumAId, $"{_uniqueSuffix}-A");
        var agentB = await AgentTestSupport.SeedAgentAsync(db, _kurumBId, $"{_uniqueSuffix}-B");

        var svcA = new AgentService(factory, new FakeKurumTenantAccessor(_kurumAId));
        var svcB = new AgentService(factory, new FakeKurumTenantAccessor(_kurumBId));

        Assert.NotNull(await svcA.GetByIdAsync(agentA.Id, CancellationToken.None));
        await Assert.ThrowsAsync<TOD.Platform.SharedKernel.Exceptions.BaseException>(() => svcB.GetByIdAsync(agentA.Id, CancellationToken.None));
        await Assert.ThrowsAsync<TOD.Platform.SharedKernel.Exceptions.BaseException>(() => svcA.DisableAsync(agentB.Id, CancellationToken.None));
        await Assert.ThrowsAsync<TOD.Platform.SharedKernel.Exceptions.BaseException>(() => svcA.RevokeAsync(agentB.Id, CancellationToken.None));

        var allA = await svcA.GetAllAsync(null, null, CancellationToken.None);
        Assert.DoesNotContain(allA, x => x.Id == agentB.Id);

        await AgentTestSupport.CleanupAsync(db, $"{_uniqueSuffix}-B");
        await AgentTestSupport.CleanupAsync(db, $"{_uniqueSuffix}-A");
    }

    [IntegrationFact]
    public async Task SuperAdmin_SeesAllKurums()
    {
        var db = await SetupAsync(); if (db is null) return;
        var factory = NewFactory();
        var agentA = await AgentTestSupport.SeedAgentAsync(db, _kurumAId, $"{_uniqueSuffix}-A");
        var agentB = await AgentTestSupport.SeedAgentAsync(db, _kurumBId, $"{_uniqueSuffix}-B");

        var svc = new AgentService(factory, new FakeSuperAdminTenantAccessor());
        Assert.NotNull(await svc.GetByIdAsync(agentA.Id, CancellationToken.None));
        Assert.NotNull(await svc.GetByIdAsync(agentB.Id, CancellationToken.None));
        var all = await svc.GetAllAsync(null, null, CancellationToken.None);
        Assert.Contains(all, x => x.Id == agentA.Id);
        Assert.Contains(all, x => x.Id == agentB.Id);

        await AgentTestSupport.CleanupAsync(db, $"{_uniqueSuffix}-B");
        await AgentTestSupport.CleanupAsync(db, $"{_uniqueSuffix}-A");
    }

    [IntegrationFact]
    public async Task KurumA_CannotCreateEnrollmentForKurumB()
    {
        var db = await SetupAsync(); if (db is null) return;
        var factory = NewFactory();
        var svc = new AgentService(factory, new FakeKurumTenantAccessor(_kurumAId));

        var req = new STYS.Agent.Contracts.Dtos.AgentEnrollmentCodeRequest { TesisIds = [_tesisBId], AllowedScopes = ["agent.heartbeat"] };
        await Assert.ThrowsAsync<TOD.Platform.SharedKernel.Exceptions.BaseException>(() => svc.GenerateEnrollmentCodeAsync(req, "test", CancellationToken.None));

        await AgentTestSupport.CleanupAsync(db, _uniqueSuffix);
    }

    // ===== 5. TESIS ISOLATION =====

    [IntegrationFact]
    public async Task Tesis_CrossKurum_Rejected()
    {
        var db = await SetupAsync(); if (db is null) return;
        var factory = NewFactory();
        var svc = new AgentService(factory, new FakeKurumTenantAccessor(_kurumAId));

        var req = new STYS.Agent.Contracts.Dtos.AgentKaydetRequest { Ad = "Test", TesisIds = [_tesisBId], Scopes = ["agent.heartbeat"] };
        await Assert.ThrowsAsync<TOD.Platform.SharedKernel.Exceptions.BaseException>(() => svc.CreateAsync(req, "test", CancellationToken.None));
        await AgentTestSupport.CleanupAsync(db, _uniqueSuffix);
    }

    [IntegrationFact]
    public async Task Tesis_Nonexistent_Rejected()
    {
        var db = await SetupAsync(); if (db is null) return;
        var factory = NewFactory();
        var svc = new AgentService(factory, new FakeSuperAdminTenantAccessor());
        var req = new STYS.Agent.Contracts.Dtos.AgentKaydetRequest { Ad = "Test", TesisIds = [99999], Scopes = ["agent.heartbeat"] };
        await Assert.ThrowsAsync<TOD.Platform.SharedKernel.Exceptions.BaseException>(() => svc.CreateAsync(req, "test", CancellationToken.None));
        await AgentTestSupport.CleanupAsync(db, _uniqueSuffix);
    }

    // ===== HELPERS =====

    private static AgentTokenService CreateTokenService(IDbContextFactory<StysAppDbContext> factory) =>
        new(factory, CreateJwtService());

    private static AgentJwtTokenService CreateJwtService() =>
        new(Microsoft.Extensions.Options.Options.Create(new TOD.Platform.Security.Auth.Options.JwtTokenOptions { Key = "01234567890123456789012345678901!!!", AccessTokenExpirationMinutes = 60 }));

    private static string ParseJwtClaim(string token, string claimType)
    {
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        return jwt.Claims.FirstOrDefault(c => c.Type == claimType)?.Value ?? string.Empty;
    }
}
