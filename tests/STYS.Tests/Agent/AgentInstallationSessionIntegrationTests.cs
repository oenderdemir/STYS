using Microsoft.EntityFrameworkCore;
using STYS.Agent.Contracts.Dtos;
using STYS.Agent.Contracts.Enums;
using STYS.Agent.Entities;
using STYS.Agent.Services;
using STYS.Infrastructure.EntityFramework;
using STYS.Tests.TestSupport;
using TOD.Platform.SharedKernel.Exceptions;
using Xunit;
using AgentEntity = STYS.Agent.Entities.Agent;

namespace STYS.Tests.Agent;

[Collection(SqlServerIntegrationCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Domain", "Agent")]
[Trait("TestLevel", "SqlIntegration")]
public sealed class AgentInstallationSessionIntegrationTests : IAsyncLifetime
{
    private const string TestMarker = "e2d1";
    private string _cs = string.Empty;
    private string _suffix = string.Empty;
    private int _kurumAId;
    private int _kurumBId;
    private int _tesisAId;
    private int _tesisBId;

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task SetupAsync()
    {
        _cs = Environment.GetEnvironmentVariable(IntegrationFactAttribute.ConnectionStringEnvVar) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(_cs))
            return;

        _suffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];
        var db = AgentTestSupport.CreateDbContext(_cs);
        var (ka, _, ta) = await AgentTestSupport.SeedKurumIlTesisAsync(db, $"{_suffix}-A");
        var (kb, _, tb) = await AgentTestSupport.SeedKurumIlTesisAsync(db, $"{_suffix}-B");
        _kurumAId = ka.Id;
        _tesisAId = ta.Id;
        _kurumBId = kb.Id;
        _tesisBId = tb.Id;
    }

    private DbContextFactoryForTest<StysAppDbContext> NewFactory() =>
        new(() => AgentTestSupport.CreateDbContext(_cs));

    [IntegrationFact]
    public async Task ValidCreate_SessionAndEnrollment_Create_And_MaxUsageIsOne()
    {
        await SetupAsync();
        if (string.IsNullOrWhiteSpace(_cs))
            return;

        var service = new AgentInstallationSessionService(NewFactory(), new FakeKurumTenantAccessor(_kurumAId));
        var response = await service.CreateAsync(new AgentInstallationSessionCreateRequest
        {
            TesisId = _tesisAId,
            AgentDisplayName = $"Session-{_suffix}",
            TargetRid = "win-x64",
            Scopes = ["agent.heartbeat", "agent.command.read", "agent.command.execute", "agent.result.write", "agent.config.read"],
            RequiresApproval = false,
            ExpirationHours = 2
        }, "tester", CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(response.EnrollmentCode));
        Assert.Equal(AgentInstallationSessionStatus.EnrollmentPending, response.Session.Status);
        Assert.Equal("win-x64", response.Session.TargetRid);
        Assert.Contains("agent.heartbeat", response.Session.Scopes);

        await using var verify = AgentTestSupport.CreateDbContext(_cs);
        var session = await verify.Set<AgentInstallationSession>().FirstAsync(x => x.Id == response.Session.Id);
        var enrollment = await verify.Set<AgentEnrollment>().FirstAsync(x => x.AgentInstallationSessionId == session.Id);

        Assert.Equal(1, enrollment.MaxKullanimSayisi);
        Assert.Equal(session.ExpiresAt, enrollment.ExpiresAt);
        Assert.Equal(AgentEnrollmentDurum.Active, enrollment.Durum);
        Assert.Equal(_kurumAId, session.KurumId);
        Assert.Equal(_tesisAId, session.TesisId);

        await AgentTestSupport.CleanupAsync(verify, _suffix);
    }

    [IntegrationFact]
    public async Task OtherKurumTesis_SessionCreate_Reddedilir()
    {
        await SetupAsync();
        if (string.IsNullOrWhiteSpace(_cs))
            return;

        var service = new AgentInstallationSessionService(NewFactory(), new FakeKurumTenantAccessor(_kurumAId));

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.CreateAsync(new AgentInstallationSessionCreateRequest
        {
            TesisId = _tesisBId,
            AgentDisplayName = $"Session-{_suffix}",
            TargetRid = "win-x64",
            Scopes = ["agent.heartbeat"]
        }, "tester", CancellationToken.None));

        Assert.Contains("tesis", ex.Message, StringComparison.OrdinalIgnoreCase);

        await using var verify = AgentTestSupport.CreateDbContext(_cs);
        await AgentTestSupport.CleanupAsync(verify, _suffix);
    }

    [IntegrationFact]
    public async Task UnknownRid_And_UnknownScope_Reddedilir()
    {
        await SetupAsync();
        if (string.IsNullOrWhiteSpace(_cs))
            return;

        var service = new AgentInstallationSessionService(NewFactory(), new FakeKurumTenantAccessor(_kurumAId));

        await Assert.ThrowsAsync<BaseException>(() => service.CreateAsync(new AgentInstallationSessionCreateRequest
        {
            TesisId = _tesisAId,
            AgentDisplayName = $"Session-{_suffix}",
            TargetRid = "macos-arm64",
            Scopes = ["agent.heartbeat"]
        }, "tester", CancellationToken.None));

        await Assert.ThrowsAsync<BaseException>(() => service.CreateAsync(new AgentInstallationSessionCreateRequest
        {
            TesisId = _tesisAId,
            AgentDisplayName = $"Session-{_suffix}",
            TargetRid = "win-x64",
            Scopes = ["agent.heartbeat", "agent.unknown"]
        }, "tester", CancellationToken.None));

        await using var verify = AgentTestSupport.CreateDbContext(_cs);
        await AgentTestSupport.CleanupAsync(verify, _suffix);
    }

    [IntegrationFact]
    public async Task CancelledSession_EnrolmenteIzinVermez()
    {
        await SetupAsync();
        if (string.IsNullOrWhiteSpace(_cs))
            return;

        var sessionService = new AgentInstallationSessionService(NewFactory(), new FakeKurumTenantAccessor(_kurumAId));
        var create = await sessionService.CreateAsync(new AgentInstallationSessionCreateRequest
        {
            TesisId = _tesisAId,
            AgentDisplayName = $"Session-{_suffix}",
            TargetRid = "win-x64",
            Scopes = ["agent.heartbeat"]
        }, "tester", CancellationToken.None);

        await sessionService.CancelAsync(create.Session.Id, "tester", CancellationToken.None);

        var tokenService = new AgentTokenService(NewFactory(), CreateJwtService());
        var ex = await Assert.ThrowsAsync<BaseException>(() => tokenService.EnrollAsync(new AgentEnrollmentRequest
        {
            EnrollmentCode = create.EnrollmentCode,
            AgentKey = $"AGNT-{_suffix}-cancel"
        }, CancellationToken.None));

        Assert.Contains("geçerli değil", ex.Message, StringComparison.OrdinalIgnoreCase);

        await using var verify = AgentTestSupport.CreateDbContext(_cs);
        await AgentTestSupport.CleanupAsync(verify, _suffix);
    }

    [IntegrationFact]
    public async Task ExpiredSession_EnrolmenteIzinVermez()
    {
        await SetupAsync();
        if (string.IsNullOrWhiteSpace(_cs))
            return;

        var sessionService = new AgentInstallationSessionService(NewFactory(), new FakeKurumTenantAccessor(_kurumAId));
        var create = await sessionService.CreateAsync(new AgentInstallationSessionCreateRequest
        {
            TesisId = _tesisAId,
            AgentDisplayName = $"Session-{_suffix}",
            TargetRid = "win-x64",
            Scopes = ["agent.heartbeat"],
            ExpirationHours = 2
        }, "tester", CancellationToken.None);

        await using (var db = AgentTestSupport.CreateDbContext(_cs))
        {
            var session = await db.Set<AgentInstallationSession>().FirstAsync(x => x.Id == create.Session.Id);
            session.ExpiresAt = DateTime.UtcNow.AddMinutes(-5);
            await db.SaveChangesAsync();
        }

        var tokenService = new AgentTokenService(NewFactory(), CreateJwtService());
        var ex = await Assert.ThrowsAsync<BaseException>(() => tokenService.EnrollAsync(new AgentEnrollmentRequest
        {
            EnrollmentCode = create.EnrollmentCode,
            AgentKey = $"AGNT-{_suffix}-expired"
        }, CancellationToken.None));

        Assert.Contains("süresi dolmuş", ex.Message, StringComparison.OrdinalIgnoreCase);

        await using var verify = AgentTestSupport.CreateDbContext(_cs);
        await AgentTestSupport.CleanupAsync(verify, _suffix);
    }

    [IntegrationFact]
    public async Task SuccessfulEnrollment_SessionEnrolledAgentIdIleGuncellenir()
    {
        await SetupAsync();
        if (string.IsNullOrWhiteSpace(_cs))
            return;

        var sessionService = new AgentInstallationSessionService(NewFactory(), new FakeKurumTenantAccessor(_kurumAId));
        var create = await sessionService.CreateAsync(new AgentInstallationSessionCreateRequest
        {
            TesisId = _tesisAId,
            AgentDisplayName = $"Session-{_suffix}",
            TargetRid = "win-x64",
            Scopes = ["agent.heartbeat", "agent.command.read", "agent.command.execute", "agent.result.write", "agent.config.read"]
        }, "tester", CancellationToken.None);

        var tokenService = new AgentTokenService(NewFactory(), CreateJwtService());
        var result = await tokenService.EnrollAsync(new AgentEnrollmentRequest
        {
            EnrollmentCode = create.EnrollmentCode,
            AgentKey = $"AGNT-{_suffix}-ok",
            AgentVersion = "1.0.0",
            PublicKey = "public-key",
            Capabilities = ["pavo"]
        }, CancellationToken.None);

        Assert.NotNull(result);

        await using var verify = AgentTestSupport.CreateDbContext(_cs);
        var session = await verify.Set<AgentInstallationSession>().FirstAsync(x => x.Id == create.Session.Id);
        var agent = await verify.Set<AgentEntity>().FirstAsync(x => x.Id == session.EnrolledAgentId);

        Assert.Equal(agent.Id, session.EnrolledAgentId);
        Assert.Equal(AgentInstallationSessionStatus.Enrolled, session.Status);
        Assert.Equal("Session-" + _suffix, agent.Ad);
        Assert.Equal("win-x64", agent.RuntimeIdentifier);

        await AgentTestSupport.CleanupAsync(verify, _suffix);
    }

    [IntegrationFact]
    public async Task RequiresApproval_AuthenticationOncePendingApproval_Olur()
    {
        await SetupAsync();
        if (string.IsNullOrWhiteSpace(_cs))
            return;

        var sessionService = new AgentInstallationSessionService(NewFactory(), new FakeKurumTenantAccessor(_kurumAId));
        var create = await sessionService.CreateAsync(new AgentInstallationSessionCreateRequest
        {
            TesisId = _tesisAId,
            AgentDisplayName = $"Session-{_suffix}",
            TargetRid = "linux-x64",
            Scopes = ["agent.heartbeat", "agent.command.read"],
            RequiresApproval = true
        }, "tester", CancellationToken.None);

        var tokenService = new AgentTokenService(NewFactory(), CreateJwtService());
        await tokenService.EnrollAsync(new AgentEnrollmentRequest
        {
            EnrollmentCode = create.EnrollmentCode,
            AgentKey = $"AGNT-{_suffix}-approval"
        }, CancellationToken.None);

        await using var verify = AgentTestSupport.CreateDbContext(_cs);
        var session = await verify.Set<AgentInstallationSession>().FirstAsync(x => x.Id == create.Session.Id);
        var enrollment = await verify.Set<AgentEnrollment>().FirstAsync(x => x.AgentInstallationSessionId == session.Id);

        Assert.Equal(AgentInstallationSessionStatus.PendingApproval, session.Status);
        Assert.Equal(AgentDurum.PendingApproval, (await verify.Set<AgentEntity>().FirstAsync(x => x.Id == session.EnrolledAgentId)).Durum);
        Assert.Equal(AgentEnrollmentDurum.Used, enrollment.Durum);

        await AgentTestSupport.CleanupAsync(verify, _suffix);
    }

    private static AgentJwtTokenService CreateJwtService() =>
        new(Microsoft.Extensions.Options.Options.Create(new TOD.Platform.Security.Auth.Options.JwtTokenOptions
        {
            Key = "01234567890123456789012345678901!!!",
            AccessTokenExpirationMinutes = 60
        }));
}
