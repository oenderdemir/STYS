using Microsoft.EntityFrameworkCore;
using STYS.Agent.Contracts.Enums;
using STYS.Agent.Entities;
using STYS.Agent.Services;
using STYS.Infrastructure.EntityFramework;
using STYS.Tests.TestSupport;
using TOD.Platform.Security.Auth.Services;
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
        _uniqueSuffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];
        var db = AgentTestSupport.CreateDbContext(cs);

        var (ka, _, ta) = await AgentTestSupport.SeedKurumIlTesisAsync(db, $"{_uniqueSuffix}-A");
        var (kb, _, tb) = await AgentTestSupport.SeedKurumIlTesisAsync(db, $"{_uniqueSuffix}-B");
        _kurumAId = ka.Id; _tesisAId = ta.Id;
        _kurumBId = kb.Id; _tesisBId = tb.Id;
        return db;
    }

    private static AgentService CreateSuperAdminService(IDbContextFactory<StysAppDbContext> factory) => new(factory, new FakeSuperAdminTenantAccessor());
    private static AgentService CreateKurumAService(IDbContextFactory<StysAppDbContext> factory) => new(factory, new FakeKurumTenantAccessor(0));
    private static AgentService CreateKurumBService(IDbContextFactory<StysAppDbContext> factory) => new(factory, new FakeKurumTenantAccessor(1));

    private static void SetKurumId(FakeKurumTenantAccessor accessor, int kurumId) => accessor.SetKurumId(kurumId);

    // ===== 1. SCOPE UPDATE TESTS =====

    [IntegrationFact]
    public async Task Scope_AddNewScope_TokenContainsNewScope()
    {
        var db = await SetupAsync(); if (db is null) return;
        var factory = new DbContextFactoryForTest<StysAppDbContext>(db);
        var service = CreateSuperAdminService(factory);

        var agent = await AgentTestSupport.SeedAgentAsync(db, _kurumAId, _uniqueSuffix);
        var (cred, _) = await AgentTestSupport.SeedCredentialAsync(db, agent.Id, _kurumAId);

        await service.UpdateScopesAsync(agent.Id, new List<string> { "agent.heartbeat", "stys.pavo.payment.execute" }, CancellationToken.None);

        var tokenService = CreateTokenService(factory);
        var token = await tokenService.IssueTokenAsync(new STYS.Agent.Contracts.Dtos.AgentTokenRequest { ClientId = cred.ClientId, ClientSecret = "test-secret-doesnt-matter-for-scope-check", AgentInstanceId = "test" }, CancellationToken.None);
        Assert.NotNull(token);

        await AgentTestSupport.CleanupAsync(db, _uniqueSuffix);
    }

    [IntegrationFact]
    public async Task Scope_RemoveScope_NewTokenDoesNotContainScope()
    {
        var db = await SetupAsync(); if (db is null) return;
        var factory = new DbContextFactoryForTest<StysAppDbContext>(db);
        var service = CreateSuperAdminService(factory);

        var agent = await AgentTestSupport.SeedAgentAsync(db, _kurumAId, _uniqueSuffix);
        await service.UpdateScopesAsync(agent.Id, new List<string> { "agent.heartbeat", "stys.pavo.payment.execute" }, CancellationToken.None);

        await service.UpdateScopesAsync(agent.Id, new List<string> { "agent.heartbeat" }, CancellationToken.None);

        var scopes = await db.Set<AgentScope>().Where(x => x.AgentId == agent.Id && !x.IsDeleted && x.AktifMi).ToListAsync();
        Assert.Single(scopes);
        Assert.Equal("agent.heartbeat", scopes[0].Scope);
        Assert.Contains(scopes, x => x.IsDeleted);

        await AgentTestSupport.CleanupAsync(db, _uniqueSuffix);
    }

    [IntegrationFact]
    public async Task Scope_ChangeInvalidatesCredentialVersion()
    {
        var db = await SetupAsync(); if (db is null) return;
        var factory = new DbContextFactoryForTest<StysAppDbContext>(db);
        var service = CreateSuperAdminService(factory);

        var agent = await AgentTestSupport.SeedAgentAsync(db, _kurumAId, _uniqueSuffix);
        var (cred, rawSecret) = await AgentTestSupport.SeedCredentialAsync(db, agent.Id, _kurumAId);
        var originalVersion = cred.CredentialVersion;

        await service.UpdateScopesAsync(agent.Id, new List<string> { "agent.heartbeat", "agent.config.read" }, CancellationToken.None);

        await db.Entry(cred).ReloadAsync();
        Assert.True(cred.CredentialVersion > originalVersion, $"Expected CredentialVersion > {originalVersion}, got {cred.CredentialVersion}");

        await AgentTestSupport.CleanupAsync(db, _uniqueSuffix);
    }

    [IntegrationFact]
    public async Task Scope_CaseInsensitiveNormalization()
    {
        var db = await SetupAsync(); if (db is null) return;
        var factory = new DbContextFactoryForTest<StysAppDbContext>(db);
        var service = CreateSuperAdminService(factory);

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
        var factory = new DbContextFactoryForTest<StysAppDbContext>(db);
        var service = CreateSuperAdminService(factory);

        var agent = await AgentTestSupport.SeedAgentAsync(db, _kurumAId, _uniqueSuffix);
        await service.UpdateScopesAsync(agent.Id, new List<string> { "agent.heartbeat" }, CancellationToken.None);
        await service.UpdateScopesAsync(agent.Id, new List<string> { "agent.heartbeat", "agent.heartbeat" }, CancellationToken.None);

        var count = await db.Set<AgentScope>().CountAsync(x => x.AgentId == agent.Id && x.Scope == "agent.heartbeat" && !x.IsDeleted && x.AktifMi);
        Assert.Equal(1, count);

        await AgentTestSupport.CleanupAsync(db, _uniqueSuffix);
    }

    // ===== 2. ENROLLMENT CONCURRENCY =====

    [IntegrationFact]
    public async Task Enrollment_ConcurrentSingleUse_CreatesOneAgent()
    {
        var db = await SetupAsync(); if (db is null) return;
        var factory = new DbContextFactoryForTest<StysAppDbContext>(db);
        var service = CreateSuperAdminService(factory);

        var codeReq = new STYS.Agent.Contracts.Dtos.AgentEnrollmentCodeRequest { KurumId = _kurumAId, TesisIds = [_tesisAId], AllowedScopes = ["agent.heartbeat"], MaxKullanimSayisi = 1, ExpirationHours = 1 };
        var enrollment = await service.GenerateEnrollmentCodeAsync(codeReq, "test", CancellationToken.None);

        var tokenService = CreateTokenService(factory);
        var requestTemplate = new STYS.Agent.Contracts.Dtos.AgentEnrollmentRequest { EnrollmentCode = enrollment.Code, AgentKey = $"AGNT-{_uniqueSuffix}", AgentVersion = "1.0" };

        var tasks = new List<Task<STYS.Agent.Contracts.Dtos.AgentEnrollmentResponse?>>();
        for (int i = 0; i < 2; i++)
        {
            var req = new STYS.Agent.Contracts.Dtos.AgentEnrollmentRequest { EnrollmentCode = requestTemplate.EnrollmentCode, AgentKey = $"{requestTemplate.AgentKey}-{i}", AgentVersion = requestTemplate.AgentVersion };
            tasks.Add(Task.Run(async () =>
            {
                try { return await tokenService.EnrollAsync(req, CancellationToken.None); }
                catch { return null; }
            }));
        }

        var results = await Task.WhenAll(tasks);
        var successCount = results.Count(x => x is not null);
        var failCount = results.Count(x => x is null);

        Assert.Equal(1, successCount);
        Assert.Equal(1, failCount);

        var agentCount = await db.Set<AgentEntity>().CountAsync(x => x.AgentKey.StartsWith($"AGNT-{_uniqueSuffix}"));
        Assert.Equal(1, agentCount);

        var updatedEnrollment = await db.Set<AgentEnrollment>().FirstAsync(x => x.Code == enrollment.Code);
        Assert.Equal(1, updatedEnrollment.KullanimSayisi);

        await AgentTestSupport.CleanupAsync(db, _uniqueSuffix);
    }

    [IntegrationFact]
    public async Task Enrollment_Concurrent_NoOrphanRecords()
    {
        var db = await SetupAsync(); if (db is null) return;
        var factory = new DbContextFactoryForTest<StysAppDbContext>(db);
        var service = CreateSuperAdminService(factory);

        var codeReq = new STYS.Agent.Contracts.Dtos.AgentEnrollmentCodeRequest { KurumId = _kurumAId, TesisIds = [_tesisAId], AllowedScopes = ["agent.heartbeat"], MaxKullanimSayisi = 1, ExpirationHours = 1 };
        var enrollment = await service.GenerateEnrollmentCodeAsync(codeReq, "test", CancellationToken.None);

        var tokenService = CreateTokenService(factory);
        var tasks = new List<Task<STYS.Agent.Contracts.Dtos.AgentEnrollmentResponse?>>();
        for (int i = 0; i < 3; i++)
        {
            var req = new STYS.Agent.Contracts.Dtos.AgentEnrollmentRequest { EnrollmentCode = enrollment.Code, AgentKey = $"AGNT-{_uniqueSuffix}-{i}", AgentVersion = "1.0" };
            tasks.Add(Task.Run(async () => { try { return await tokenService.EnrollAsync(req, CancellationToken.None); } catch { return null; } }));
        }
        await Task.WhenAll(tasks);

        var successCount = tasks.Count(x => x.Result is not null);
        Assert.Equal(1, successCount);

        var agentCount = await db.Set<AgentEntity>().CountAsync(x => x.AgentKey.Contains(_uniqueSuffix));
        Assert.Equal(1, agentCount);

        var credentialCount = await db.Set<AgentCredential>().CountAsync(x => db.Set<AgentEntity>().Any(a => a.AgentKey.Contains(_uniqueSuffix) && a.Id == x.AgentId));
        Assert.Equal(1, credentialCount);

        await AgentTestSupport.CleanupAsync(db, _uniqueSuffix);
    }

    // ===== 3. REQUIRES APPROVAL =====

    [IntegrationFact]
    public async Task RequiresApproval_True_PendingAgentCannotGetToken()
    {
        var db = await SetupAsync(); if (db is null) return;
        var factory = new DbContextFactoryForTest<StysAppDbContext>(db);
        var service = CreateSuperAdminService(factory);

        var codeReq = new STYS.Agent.Contracts.Dtos.AgentEnrollmentCodeRequest { KurumId = _kurumAId, TesisIds = [_tesisAId], AllowedScopes = ["agent.heartbeat"], MaxKullanimSayisi = 1, RequiresApproval = true };
        var enrollment = await service.GenerateEnrollmentCodeAsync(codeReq, "test", CancellationToken.None);

        var tokenService = CreateTokenService(factory);
        var enrollResult = await tokenService.EnrollAsync(new STYS.Agent.Contracts.Dtos.AgentEnrollmentRequest { EnrollmentCode = enrollment.Code, AgentKey = $"PENDING-{_uniqueSuffix}" }, CancellationToken.None);

        Assert.Equal((int)AgentDurum.PendingApproval, enrollResult.Durum);
        var agent = await db.Set<AgentEntity>().FindAsync(enrollResult.AgentId);
        Assert.Equal(AgentDurum.PendingApproval, agent!.Durum);

        var ex = await Assert.ThrowsAsync<TOD.Platform.SharedKernel.Exceptions.BaseException>(() =>
            tokenService.IssueTokenAsync(new STYS.Agent.Contracts.Dtos.AgentTokenRequest { ClientId = enrollResult.ClientId, ClientSecret = enrollResult.ClientSecret, AgentInstanceId = "test" }, CancellationToken.None));
        Assert.Equal(403, ex.ErrorCode);

        await service.ApproveAsync(enrollResult.AgentId, CancellationToken.None);
        var token = await tokenService.IssueTokenAsync(new STYS.Agent.Contracts.Dtos.AgentTokenRequest { ClientId = enrollResult.ClientId, ClientSecret = enrollResult.ClientSecret, AgentInstanceId = "test" }, CancellationToken.None);
        Assert.NotNull(token);

        await AgentTestSupport.CleanupAsync(db, _uniqueSuffix);
    }

    [IntegrationFact]
    public async Task RequiresApproval_False_AgentIsActiveImmediately()
    {
        var db = await SetupAsync(); if (db is null) return;
        var factory = new DbContextFactoryForTest<StysAppDbContext>(db);
        var service = CreateSuperAdminService(factory);

        var codeReq = new STYS.Agent.Contracts.Dtos.AgentEnrollmentCodeRequest { KurumId = _kurumAId, TesisIds = [_tesisAId], AllowedScopes = ["agent.heartbeat"], MaxKullanimSayisi = 1, RequiresApproval = false };
        var enrollment = await service.GenerateEnrollmentCodeAsync(codeReq, "test", CancellationToken.None);

        var tokenService = CreateTokenService(factory);
        var result = await tokenService.EnrollAsync(new STYS.Agent.Contracts.Dtos.AgentEnrollmentRequest { EnrollmentCode = enrollment.Code, AgentKey = $"ACTIVE-{_uniqueSuffix}" }, CancellationToken.None);
        Assert.Equal((int)AgentDurum.Active, result.Durum);

        await AgentTestSupport.CleanupAsync(db, _uniqueSuffix);
    }

    // ===== 4. KURUM ISOLATION =====

    [IntegrationFact]
    public async Task KurumA_Admin_CannotAccessKurumB_Agent()
    {
        var db = await SetupAsync(); if (db is null) return;
        var factory = new DbContextFactoryForTest<StysAppDbContext>(db);

        var agentA = await AgentTestSupport.SeedAgentAsync(db, _kurumAId, $"{_uniqueSuffix}-A");
        var agentB = await AgentTestSupport.SeedAgentAsync(db, _kurumBId, $"{_uniqueSuffix}-B");

        var accessorA = new FakeKurumTenantAccessor(_kurumAId);
        var serviceA = new AgentService(factory, accessorA);
        var serviceB_ = new AgentService(factory, new FakeKurumTenantAccessor(_kurumBId));

        var result = await serviceA.GetByIdAsync(agentA.Id, CancellationToken.None);
        Assert.NotNull(result);

        var exDetail = await Assert.ThrowsAsync<TOD.Platform.SharedKernel.Exceptions.BaseException>(() => serviceB_.GetByIdAsync(agentA.Id, CancellationToken.None));
        Assert.Equal(403, exDetail.ErrorCode);

        var exDisable = await Assert.ThrowsAsync<TOD.Platform.SharedKernel.Exceptions.BaseException>(() => serviceA.DisableAsync(agentB.Id, CancellationToken.None));
        Assert.Equal(403, exDisable.ErrorCode);

        var exRevoke = await Assert.ThrowsAsync<TOD.Platform.SharedKernel.Exceptions.BaseException>(() => serviceA.RevokeAsync(agentB.Id, CancellationToken.None));
        Assert.Equal(403, exRevoke.ErrorCode);

        await AgentTestSupport.CleanupAsync(db, $"{_uniqueSuffix}-B");
        await AgentTestSupport.CleanupAsync(db, $"{_uniqueSuffix}-A");
    }

    [IntegrationFact]
    public async Task SuperAdmin_CanAccessAllKurums()
    {
        var db = await SetupAsync(); if (db is null) return;
        var factory = new DbContextFactoryForTest<StysAppDbContext>(db);

        var agentA = await AgentTestSupport.SeedAgentAsync(db, _kurumAId, $"{_uniqueSuffix}-A");
        var agentB = await AgentTestSupport.SeedAgentAsync(db, _kurumBId, $"{_uniqueSuffix}-B");

        var superAdmin = CreateSuperAdminService(factory);
        var mA = await superAdmin.GetByIdAsync(agentA.Id, CancellationToken.None);
        var mB = await superAdmin.GetByIdAsync(agentB.Id, CancellationToken.None);
        Assert.NotNull(mA); Assert.NotNull(mB);

        var all = await superAdmin.GetAllAsync(CancellationToken.None);
        Assert.Contains(all, x => x.Id == agentA.Id);
        Assert.Contains(all, x => x.Id == agentB.Id);

        await AgentTestSupport.CleanupAsync(db, $"{_uniqueSuffix}-B");
        await AgentTestSupport.CleanupAsync(db, $"{_uniqueSuffix}-A");
    }

    // ===== 5. TESIS ISOLATION =====

    [IntegrationFact]
    public async Task Tesis_CrossKurumTesis_Rejected()
    {
        var db = await SetupAsync(); if (db is null) return;
        var factory = new DbContextFactoryForTest<StysAppDbContext>(db);
        var service = CreateSuperAdminService(factory);

        var request = new STYS.Agent.Contracts.Dtos.AgentKaydetRequest { Ad = "Test", KurumId = _kurumAId, TesisIds = [_tesisBId], Scopes = ["agent.heartbeat"] };
        var ex = await Assert.ThrowsAsync<TOD.Platform.SharedKernel.Exceptions.BaseException>(() => service.CreateAsync(request, "test", CancellationToken.None));
        Assert.Equal(400, ex.ErrorCode);

        await AgentTestSupport.CleanupAsync(db, _uniqueSuffix);
    }

    [IntegrationFact]
    public async Task Tesis_NonexistentTesis_Rejected()
    {
        var db = await SetupAsync(); if (db is null) return;
        var factory = new DbContextFactoryForTest<StysAppDbContext>(db);
        var service = CreateSuperAdminService(factory);

        var request = new STYS.Agent.Contracts.Dtos.AgentKaydetRequest { Ad = "Test", KurumId = _kurumAId, TesisIds = [99999], Scopes = ["agent.heartbeat"] };
        await Assert.ThrowsAsync<TOD.Platform.SharedKernel.Exceptions.BaseException>(() => service.CreateAsync(request, "test", CancellationToken.None));

        await AgentTestSupport.CleanupAsync(db, _uniqueSuffix);
    }

    // ===== HELPERS =====

    private static AgentTokenService CreateTokenService(IDbContextFactory<StysAppDbContext> factory)
    {
        var jwtOptions = Microsoft.Extensions.Options.Options.Create(new TOD.Platform.Security.Auth.Options.JwtTokenOptions { Key = "01234567890123456789012345678901!!!", AccessTokenExpirationMinutes = 60 });
        var jwtService = new AgentJwtTokenService(jwtOptions);
        return new AgentTokenService(factory, jwtService);
    }
}
