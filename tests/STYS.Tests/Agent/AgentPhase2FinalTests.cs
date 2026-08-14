using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging.Abstractions;
using STYS.Agent.Contracts.Enums;
using STYS.Agent.Entities;
using STYS.Agent.Services;
using STYS.Infrastructure.EntityFramework;
using STYS.Tests.TestSupport;
using Xunit;

namespace STYS.Tests.Agent;

[Collection(SqlServerIntegrationCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Domain", "Agent")]
[Trait("TestLevel", "SqlIntegration")]
public sealed class AgentPhase2FinalTests : IAsyncLifetime
{
    private const string TestMarker = "ph2fin";
    private string _uniqueSuffix = string.Empty;
    private string _cs = string.Empty;
    private int _kurumId;
    private int _tesisId;

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<StysAppDbContext> SetupAsync()
    {
        _cs = Environment.GetEnvironmentVariable(IntegrationFactAttribute.ConnectionStringEnvVar);
        if (string.IsNullOrWhiteSpace(_cs)) return null!;
        _uniqueSuffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];
        var db = AgentTestSupport.CreateDbContext(_cs);
        var (ka, _, ta) = await AgentTestSupport.SeedKurumIlTesisAsync(db, _uniqueSuffix);
        _kurumId = ka.Id; _tesisId = ta.Id;
        return db;
    }

    private DbContextFactoryForTest<StysAppDbContext> NewFactory() => new(() => AgentTestSupport.CreateDbContext(_cs));

    private async Task<int> SeedAgentWithScopeAsync(StysAppDbContext db, string scope)
    {
        var agent = await AgentTestSupport.SeedAgentAsync(db, _kurumId, _uniqueSuffix);
        if (!await db.Set<AgentScope>().AnyAsync(x => x.AgentId == agent.Id && x.Scope == scope && !x.IsDeleted))
        {
            db.Set<AgentScope>().Add(new AgentScope { AgentId = agent.Id, KurumId = agent.KurumId, Scope = scope, AktifMi = true, CreatedBy = "test", CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        return agent.Id;
    }

    [IntegrationFact]
    public async Task Transition_DeliveredToAcceptedToRunningToCompleted_Passes()
    {
        var db = await SetupAsync(); if (db is null) return;
        var factory = NewFactory();
        var svc = new AgentCommandService(factory, new FakeSuperAdminTenantAccessor(), NullLogger<AgentCommandService>.Instance);
        var agentId = await SeedAgentWithScopeAsync(db, "agent.command.execute");

        var cmd = await svc.SendAsync(new STYS.Agent.Contracts.Dtos.AgentCommandSendRequest { AgentId = agentId, CommandType = "Ping", Priority = 1 }, "test", CancellationToken.None);
        var pending = await svc.GetPendingCommandsAsync(agentId, CancellationToken.None);
        Assert.Single(pending);
        var leaseToken = pending.Single().LeaseToken!;

        await svc.AcceptAsync(cmd.Id, agentId, CancellationToken.None);
        await svc.SetRunningAsync(cmd.Id, agentId, CancellationToken.None);
        await svc.CompleteAsync(cmd.Id, agentId, new STYS.Agent.Contracts.Dtos.AgentCommandCompleteRequest { Id = cmd.Id, Success = true, LeaseToken = leaseToken }, CancellationToken.None);

        var updated = await db.Set<AgentCommand>().FirstOrDefaultAsync(x => x.Id == cmd.Id);
        Assert.Equal(AgentCommandStatus.Completed, updated!.Status);

        var exs = await db.Set<AgentCommandExecution>().Where(x => x.CommandId == cmd.Id).OrderBy(x => x.Id).ToListAsync();
        Assert.Equal(3, exs.Count);
        Assert.Equal("Accepted", exs[0].Status); Assert.Equal("Delivered", exs[0].PreviousStatus);
        Assert.Equal("Running", exs[1].Status); Assert.Equal("Accepted", exs[1].PreviousStatus);
        Assert.Equal("Completed", exs[2].Status); Assert.Equal("Running", exs[2].PreviousStatus);

        await AgentTestSupport.CleanupAsync(db, _uniqueSuffix);
    }

    [IntegrationFact]
    public async Task Transition_AcceptedToCompletedDirectly_Fails()
    {
        var db = await SetupAsync(); if (db is null) return;
        var factory = NewFactory();
        var svc = new AgentCommandService(factory, new FakeSuperAdminTenantAccessor(), NullLogger<AgentCommandService>.Instance);
        var agentId = await SeedAgentWithScopeAsync(db, "agent.command.execute");

        var cmd = await svc.SendAsync(new STYS.Agent.Contracts.Dtos.AgentCommandSendRequest { AgentId = agentId, CommandType = "Ping", Priority = 1 }, "test", CancellationToken.None);
        await svc.GetPendingCommandsAsync(agentId, CancellationToken.None);
        var leaseToken = (await db.Set<AgentCommand>().Where(x => x.Id == cmd.Id).Select(x => x.LeaseToken).SingleAsync())!;
        await svc.AcceptAsync(cmd.Id, agentId, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CompleteAsync(cmd.Id, agentId, new STYS.Agent.Contracts.Dtos.AgentCommandCompleteRequest { Id = cmd.Id, Success = true, LeaseToken = leaseToken }, CancellationToken.None));

        await AgentTestSupport.CleanupAsync(db, _uniqueSuffix);
    }

    [IntegrationFact]
    public async Task Transition_RunningToFailed_Passes()
    {
        var db = await SetupAsync(); if (db is null) return;
        var factory = NewFactory();
        var svc = new AgentCommandService(factory, new FakeSuperAdminTenantAccessor(), NullLogger<AgentCommandService>.Instance);
        var agentId = await SeedAgentWithScopeAsync(db, "agent.command.execute");

        var cmd = await svc.SendAsync(new STYS.Agent.Contracts.Dtos.AgentCommandSendRequest { AgentId = agentId, CommandType = "Ping", Priority = 1 }, "test", CancellationToken.None);
        await svc.GetPendingCommandsAsync(agentId, CancellationToken.None);
        var leaseToken = (await db.Set<AgentCommand>().Where(x => x.Id == cmd.Id).Select(x => x.LeaseToken).SingleAsync())!;
        await svc.AcceptAsync(cmd.Id, agentId, CancellationToken.None);
        await svc.SetRunningAsync(cmd.Id, agentId, CancellationToken.None);
        await svc.FailAsync(cmd.Id, agentId, "test-error", CancellationToken.None);

        var updated = await db.Set<AgentCommand>().FirstOrDefaultAsync(x => x.Id == cmd.Id);
        Assert.Equal(AgentCommandStatus.Failed, updated!.Status);

        var exs = await db.Set<AgentCommandExecution>().Where(x => x.CommandId == cmd.Id).OrderBy(x => x.Id).ToListAsync();
        var failEx = exs.Last();
        Assert.Equal("Failed", failEx.Status);
        Assert.Equal("Running", failEx.PreviousStatus);

        await AgentTestSupport.CleanupAsync(db, _uniqueSuffix);
    }

    [IntegrationFact]
    public async Task Transition_DeliveredToFailed_Passes()
    {
        var db = await SetupAsync(); if (db is null) return;
        var factory = NewFactory();
        var svc = new AgentCommandService(factory, new FakeSuperAdminTenantAccessor(), NullLogger<AgentCommandService>.Instance);
        var agentId = await SeedAgentWithScopeAsync(db, "agent.command.execute");

        var cmd = await svc.SendAsync(new STYS.Agent.Contracts.Dtos.AgentCommandSendRequest { AgentId = agentId, CommandType = "Ping", Priority = 1 }, "test", CancellationToken.None);
        await svc.GetPendingCommandsAsync(agentId, CancellationToken.None);
        await svc.FailAsync(cmd.Id, agentId, "delivery-error", CancellationToken.None);

        var updated = await db.Set<AgentCommand>().FirstOrDefaultAsync(x => x.Id == cmd.Id);
        Assert.Equal(AgentCommandStatus.Failed, updated!.Status);

        var ex = await db.Set<AgentCommandExecution>().Where(x => x.CommandId == cmd.Id).OrderBy(x => x.Id).LastAsync();
        Assert.Equal("Failed", ex.Status);
        Assert.Equal("Delivered", ex.PreviousStatus);

        await AgentTestSupport.CleanupAsync(db, _uniqueSuffix);
    }

    [IntegrationFact]
    public async Task Transition_FromTerminalState_Fails()
    {
        var db = await SetupAsync(); if (db is null) return;
        var factory = NewFactory();
        var svc = new AgentCommandService(factory, new FakeSuperAdminTenantAccessor(), NullLogger<AgentCommandService>.Instance);
        var agentId = await SeedAgentWithScopeAsync(db, "agent.command.execute");

        var cmd = await svc.SendAsync(new STYS.Agent.Contracts.Dtos.AgentCommandSendRequest { AgentId = agentId, CommandType = "Ping", Priority = 1 }, "test", CancellationToken.None);
        await svc.GetPendingCommandsAsync(agentId, CancellationToken.None);
        var leaseToken = (await db.Set<AgentCommand>().Where(x => x.Id == cmd.Id).Select(x => x.LeaseToken).SingleAsync())!;
        await svc.AcceptAsync(cmd.Id, agentId, CancellationToken.None);
        await svc.SetRunningAsync(cmd.Id, agentId, CancellationToken.None);
        await svc.CompleteAsync(cmd.Id, agentId, new STYS.Agent.Contracts.Dtos.AgentCommandCompleteRequest { Id = cmd.Id, Success = true, LeaseToken = leaseToken }, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.AcceptAsync(cmd.Id, agentId, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.SetRunningAsync(cmd.Id, agentId, CancellationToken.None));

        await AgentTestSupport.CleanupAsync(db, _uniqueSuffix);
    }

    [IntegrationFact]
    public async Task Polling_TwoParallel_OnlyOneGetsCommand()
    {
        var db = await SetupAsync(); if (db is null) return;
        var factory = NewFactory();
        var svc = new AgentCommandService(factory, new FakeSuperAdminTenantAccessor(), NullLogger<AgentCommandService>.Instance);
        var agentId = await SeedAgentWithScopeAsync(db, "agent.command.execute");

        var cmd = await svc.SendAsync(new STYS.Agent.Contracts.Dtos.AgentCommandSendRequest { AgentId = agentId, CommandType = "Ping", Priority = 1 }, "test", CancellationToken.None);

        int totalDeliveries = 0;
        var tasks = new Task<int>[2];
        for (int i = 0; i < 2; i++)
        {
            tasks[i] = Task.Run(async () =>
            {
                var f = new DbContextFactoryForTest<StysAppDbContext>(() => AgentTestSupport.CreateDbContext(_cs));
                var s = new AgentCommandService(f, new FakeSuperAdminTenantAccessor(), NullLogger<AgentCommandService>.Instance);
                var r = await s.GetPendingCommandsAsync(agentId, CancellationToken.None);
                return r.Count;
            });
        }
        var results = await Task.WhenAll(tasks);
        foreach (var c in results) Interlocked.Add(ref totalDeliveries, c);

        Assert.Equal(1, totalDeliveries);

        var updated = await db.Set<AgentCommand>().FirstOrDefaultAsync(x => x.Id == cmd.Id);
        Assert.Equal(AgentCommandStatus.Delivered, updated!.Status);

        await AgentTestSupport.CleanupAsync(db, _uniqueSuffix);
    }

    [IntegrationFact]
    public async Task Fail_PreviousStatusIsCorrect()
    {
        var db = await SetupAsync(); if (db is null) return;
        var factory = NewFactory();
        var svc = new AgentCommandService(factory, new FakeSuperAdminTenantAccessor(), NullLogger<AgentCommandService>.Instance);
        var agentId = await SeedAgentWithScopeAsync(db, "agent.command.execute");

        var cmd = await svc.SendAsync(new STYS.Agent.Contracts.Dtos.AgentCommandSendRequest { AgentId = agentId, CommandType = "Ping", Priority = 1 }, "test", CancellationToken.None);
        await svc.GetPendingCommandsAsync(agentId, CancellationToken.None);
        await svc.AcceptAsync(cmd.Id, agentId, CancellationToken.None);
        await svc.SetRunningAsync(cmd.Id, agentId, CancellationToken.None);
        await svc.FailAsync(cmd.Id, agentId, "crash", CancellationToken.None);

        var ex = await db.Set<AgentCommandExecution>().Where(x => x.CommandId == cmd.Id).OrderBy(x => x.Id).LastAsync();
        Assert.Equal("Failed", ex.Status);
        Assert.Equal("Running", ex.PreviousStatus);

        await AgentTestSupport.CleanupAsync(db, _uniqueSuffix);
    }

    [IntegrationFact]
    public async Task Reject_PreviousStatusIsCorrect()
    {
        var db = await SetupAsync(); if (db is null) return;
        var factory = NewFactory();
        var svc = new AgentCommandService(factory, new FakeSuperAdminTenantAccessor(), NullLogger<AgentCommandService>.Instance);
        var agentId = await SeedAgentWithScopeAsync(db, "agent.command.execute");

        var cmd = await svc.SendAsync(new STYS.Agent.Contracts.Dtos.AgentCommandSendRequest { AgentId = agentId, CommandType = "Ping", Priority = 1 }, "test", CancellationToken.None);
        // Reject directly from Pending (unknown command scenario)
        await svc.RejectAsync(cmd.Id, agentId, "unknown-type", CancellationToken.None);

        var ex = await db.Set<AgentCommandExecution>().Where(x => x.CommandId == cmd.Id).OrderBy(x => x.Id).LastAsync();
        Assert.Equal("Rejected", ex.Status);
        Assert.Equal("Pending", ex.PreviousStatus);

        await AgentTestSupport.CleanupAsync(db, _uniqueSuffix);
    }

    [IntegrationFact]
    public async Task ApplyUpgrade_ExpiredCompletion_SettlesToCompleted_AndDuplicateIsIgnored()
    {
        var db = await SetupAsync(); if (db is null) return;
        var factory = NewFactory();
        var svc = new AgentCommandService(factory, new FakeSuperAdminTenantAccessor(), NullLogger<AgentCommandService>.Instance);
        var agentId = await SeedAgentWithScopeAsync(db, "agent.command.execute");
        var commandId = await SeedExpiredApplyCommandAsync(db, agentId, AgentCommandStatus.Expired);
        var leaseToken = await db.Set<AgentCommand>().Where(x => x.Id == commandId).Select(x => x.LeaseToken).SingleAsync();

        await svc.CompleteAsync(commandId, agentId, new STYS.Agent.Contracts.Dtos.AgentCommandCompleteRequest { Id = commandId, Success = true, LeaseToken = leaseToken! }, CancellationToken.None);
        await svc.CompleteAsync(commandId, agentId, new STYS.Agent.Contracts.Dtos.AgentCommandCompleteRequest { Id = commandId, Success = true, LeaseToken = leaseToken! }, CancellationToken.None);

        var updated = await db.Set<AgentCommand>().FirstOrDefaultAsync(x => x.Id == commandId);
        Assert.Equal(AgentCommandStatus.Completed, updated!.Status);

        var executions = await db.Set<AgentCommandExecution>().Where(x => x.CommandId == commandId).ToListAsync();
        Assert.Single(executions);

        await AgentTestSupport.CleanupAsync(db, _uniqueSuffix);
    }

    [IntegrationFact]
    public async Task ApplyUpgrade_ExpiredFailure_SettlesToFailed_AndDuplicateIsIgnored()
    {
        var db = await SetupAsync(); if (db is null) return;
        var factory = NewFactory();
        var svc = new AgentCommandService(factory, new FakeSuperAdminTenantAccessor(), NullLogger<AgentCommandService>.Instance);
        var agentId = await SeedAgentWithScopeAsync(db, "agent.command.execute");
        var commandId = await SeedExpiredApplyCommandAsync(db, agentId, AgentCommandStatus.Expired);
        var leaseToken = await db.Set<AgentCommand>().Where(x => x.Id == commandId).Select(x => x.LeaseToken).SingleAsync();

        await svc.FailAsync(commandId, agentId, "apply-failed", CancellationToken.None);
        await svc.FailAsync(commandId, agentId, "apply-failed", CancellationToken.None);

        var updated = await db.Set<AgentCommand>().FirstOrDefaultAsync(x => x.Id == commandId);
        Assert.Equal(AgentCommandStatus.Failed, updated!.Status);

        var executions = await db.Set<AgentCommandExecution>().Where(x => x.CommandId == commandId).ToListAsync();
        Assert.Single(executions);

        await AgentTestSupport.CleanupAsync(db, _uniqueSuffix);
    }

    [IntegrationFact]
    public async Task ApplyUpgrade_FinalSuccess_IsNotReplacedByLateFailure()
    {
        var db = await SetupAsync(); if (db is null) return;
        var factory = NewFactory();
        var svc = new AgentCommandService(factory, new FakeSuperAdminTenantAccessor(), NullLogger<AgentCommandService>.Instance);
        var agentId = await SeedAgentWithScopeAsync(db, "agent.command.execute");
        var commandId = await SeedExpiredApplyCommandAsync(db, agentId, AgentCommandStatus.Expired);
        var leaseToken = await db.Set<AgentCommand>().Where(x => x.Id == commandId).Select(x => x.LeaseToken).SingleAsync();

        await svc.CompleteAsync(commandId, agentId, new STYS.Agent.Contracts.Dtos.AgentCommandCompleteRequest { Id = commandId, Success = true, LeaseToken = leaseToken! }, CancellationToken.None);
        await svc.FailAsync(commandId, agentId, "late-failure", CancellationToken.None);

        var updated = await db.Set<AgentCommand>().FirstOrDefaultAsync(x => x.Id == commandId);
        Assert.Equal(AgentCommandStatus.Completed, updated!.Status);

        var executions = await db.Set<AgentCommandExecution>().Where(x => x.CommandId == commandId).ToListAsync();
        Assert.Single(executions);

        await AgentTestSupport.CleanupAsync(db, _uniqueSuffix);
    }

    [IntegrationFact]
    public async Task ApplyUpgrade_WrongOrMissingLeaseToken_Rejects()
    {
        var db = await SetupAsync(); if (db is null) return;
        var factory = NewFactory();
        var svc = new AgentCommandService(factory, new FakeSuperAdminTenantAccessor(), NullLogger<AgentCommandService>.Instance);
        var agentId = await SeedAgentWithScopeAsync(db, "agent.command.execute");
        var commandId = await SeedExpiredApplyCommandAsync(db, agentId, AgentCommandStatus.Expired);

        await Assert.ThrowsAsync<TOD.Platform.SharedKernel.Exceptions.BaseException>(() =>
            svc.CompleteAsync(commandId, agentId, new STYS.Agent.Contracts.Dtos.AgentCommandCompleteRequest { Id = commandId, Success = true, LeaseToken = "wrong-token" }, CancellationToken.None));

        await Assert.ThrowsAsync<TOD.Platform.SharedKernel.Exceptions.BaseException>(() =>
            svc.CompleteAsync(commandId, agentId, new STYS.Agent.Contracts.Dtos.AgentCommandCompleteRequest { Id = commandId, Success = true, LeaseToken = string.Empty }, CancellationToken.None));

        await AgentTestSupport.CleanupAsync(db, _uniqueSuffix);
    }

    [IntegrationFact]
    public async Task History_DoesNotExposeLeaseToken()
    {
        var db = await SetupAsync(); if (db is null) return;
        var factory = NewFactory();
        var svc = new AgentCommandService(factory, new FakeSuperAdminTenantAccessor(), NullLogger<AgentCommandService>.Instance);
        var agentId = await SeedAgentWithScopeAsync(db, "agent.command.execute");

        var cmd = await svc.SendAsync(new STYS.Agent.Contracts.Dtos.AgentCommandSendRequest { AgentId = agentId, CommandType = "Ping", Priority = 1, IdempotencyKey = Guid.NewGuid().ToString("N") }, "test", CancellationToken.None);
        await svc.GetPendingCommandsAsync(agentId, CancellationToken.None);

        var history = await svc.GetHistoryAsync(agentId, CancellationToken.None);
        var row = history.Single(x => x.Id == cmd.Id);
        Assert.Null(row.LeaseToken);

        await AgentTestSupport.CleanupAsync(db, _uniqueSuffix);
    }

    [IntegrationFact]
    public async Task SendAsync_ConcurrentSameIdempotencyKey_TekCommandOlusur_vePublicResponseRedacted()
    {
        var db = await SetupAsync(); if (db is null) return;
        var factory = NewFactory();
        var notifier = new CaptureNotifier();
        var svc1 = new AgentCommandService(factory, new FakeSuperAdminTenantAccessor(), NullLogger<AgentCommandService>.Instance, notifier);
        var svc2 = new AgentCommandService(factory, new FakeSuperAdminTenantAccessor(), NullLogger<AgentCommandService>.Instance);
        var agentId = await SeedAgentWithScopeAsync(db, "agent.command.execute");
        var key = Guid.NewGuid().ToString("N");

        var firstTask = Task.Run(() => svc1.SendAsync(new STYS.Agent.Contracts.Dtos.AgentCommandSendRequest
        {
            AgentId = agentId,
            CommandType = "Ping",
            Priority = 1,
            IdempotencyKey = key
        }, "test", CancellationToken.None));

        var secondTask = Task.Run(() => svc2.SendAsync(new STYS.Agent.Contracts.Dtos.AgentCommandSendRequest
        {
            AgentId = agentId,
            CommandType = "Ping",
            Priority = 1,
            IdempotencyKey = key
        }, "test", CancellationToken.None));

        var results = await Task.WhenAll(firstTask, secondTask);
        Assert.Equal(results[0].Id, results[1].Id);
        Assert.All(results, x =>
        {
            Assert.Null(x.LeaseToken);
            Assert.Null(x.LeaseExpiresAt);
        });

        var commands = await db.Set<AgentCommand>().Where(x => x.AgentId == agentId && x.IdempotencyKey == key && !x.IsDeleted).ToListAsync();
        Assert.Single(commands);
        Assert.NotEmpty(notifier.Events);
        Assert.Null(notifier.Events.Single().LeaseToken);
        Assert.Null(notifier.Events.Single().LeaseExpiresAt);

        await AgentTestSupport.CleanupAsync(db, _uniqueSuffix);
    }

    [IntegrationFact]
    public async Task GetPendingCommands_LeaseTokenPollKanalindaKalir()
    {
        var db = await SetupAsync(); if (db is null) return;
        var factory = NewFactory();
        var svc = new AgentCommandService(factory, new FakeSuperAdminTenantAccessor(), NullLogger<AgentCommandService>.Instance);
        var agentId = await SeedAgentWithScopeAsync(db, "agent.command.execute");

        await svc.SendAsync(new STYS.Agent.Contracts.Dtos.AgentCommandSendRequest { AgentId = agentId, CommandType = "Ping", Priority = 1 }, "test", CancellationToken.None);
        var pending = await svc.GetPendingCommandsAsync(agentId, CancellationToken.None);

        Assert.Single(pending);
        Assert.NotNull(pending.Single().LeaseToken);
        Assert.False(string.IsNullOrWhiteSpace(pending.Single().LeaseToken));

        await AgentTestSupport.CleanupAsync(db, _uniqueSuffix);
    }

    [IntegrationFact]
    public async Task Idempotent_SecondExecuteBlocked()
    {
        var db = await SetupAsync(); if (db is null) return;
        var factory = NewFactory();
        var svc = new AgentCommandService(factory, new FakeSuperAdminTenantAccessor(), NullLogger<AgentCommandService>.Instance);
        var agentId = await SeedAgentWithScopeAsync(db, "agent.command.execute");

        var cmd = await svc.SendAsync(new STYS.Agent.Contracts.Dtos.AgentCommandSendRequest { AgentId = agentId, CommandType = "Ping", Priority = 1 }, "test", CancellationToken.None);
        await svc.GetPendingCommandsAsync(agentId, CancellationToken.None);
        await svc.AcceptAsync(cmd.Id, agentId, CancellationToken.None);
        await svc.SetRunningAsync(cmd.Id, agentId, CancellationToken.None);
        var leaseToken = (await db.Set<AgentCommand>().Where(x => x.Id == cmd.Id).Select(x => x.LeaseToken).SingleAsync())!;
        await svc.CompleteAsync(cmd.Id, agentId, new STYS.Agent.Contracts.Dtos.AgentCommandCompleteRequest { Id = cmd.Id, Success = true, LeaseToken = leaseToken }, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.AcceptAsync(cmd.Id, agentId, CancellationToken.None));

        await AgentTestSupport.CleanupAsync(db, _uniqueSuffix);
    }

    private async Task<Guid> SeedExpiredApplyCommandAsync(StysAppDbContext db, int agentId, AgentCommandStatus status)
    {
        var leaseToken = Guid.NewGuid().ToString("N");
        var command = new AgentCommand
        {
            AgentId = agentId,
            KurumId = _kurumId,
            ReleaseId = 1,
            CommandType = "AgentApplyUpgrade",
            Status = status,
            Priority = 1,
            LeaseToken = leaseToken,
            LeaseExpiresAt = DateTime.UtcNow.AddMinutes(-1),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-5),
            CorrelationId = Guid.NewGuid().ToString("N"),
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            RequestedBy = "test",
            CreatedBy = "test",
            CreatedAt = DateTime.UtcNow.AddMinutes(-10)
        };

        db.Set<AgentCommand>().Add(command);
        await db.SaveChangesAsync();
        return command.Id;
    }

    private sealed class CaptureNotifier : IAgentCommandRealtimeNotifier
    {
        private readonly ConcurrentQueue<STYS.Agent.Contracts.Dtos.AgentCommandDto> _events = new();
        public IReadOnlyCollection<STYS.Agent.Contracts.Dtos.AgentCommandDto> Events => _events.ToArray();

        public Task CommandUpdatedAsync(STYS.Agent.Contracts.Dtos.AgentCommandDto command, CancellationToken cancellationToken)
        {
            _events.Enqueue(command);
            return Task.CompletedTask;
        }
    }
}
