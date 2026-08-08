using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using STYS.Agent.Contracts.Dtos;
using STYS.Agent.Entities;
using STYS.Agent.Services;
using STYS.Infrastructure.EntityFramework;
using TOD.Platform.Security.Auth.Services;
using Microsoft.Extensions.Logging;
using AgentEntity = STYS.Agent.Entities.Agent;

namespace STYS.Agent.Hubs;

public sealed class AgentHub : Hub
{
    public const string HubRoute = "/ui/agent-hub";
    public const string EventName = "AgentCommandUpdated";

    private readonly IDbContextFactory<StysAppDbContext> _dbFactory;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public AgentHub(IDbContextFactory<StysAppDbContext> dbFactory, ICurrentTenantAccessor tenantAccessor)
    {
        _dbFactory = dbFactory;
        _tenantAccessor = tenantAccessor;
    }

    public async Task JoinAgentGroupAsync(int agentId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(Context.ConnectionAborted);

        var agent = await db.Set<AgentEntity>()
            .FirstOrDefaultAsync(x => x.Id == agentId && !x.IsDeleted, Context.ConnectionAborted);

        if (agent is null)
            throw new HubException("Agent bulunamadı.");

        if (!_tenantAccessor.IsSuperAdmin())
        {
            var accessible = _tenantAccessor.GetAccessibleKurumIds();
            if (!accessible.Contains(agent.KurumId))
                throw new HubException("Bu agent'a erişim yetkiniz yok.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GetAgentGroupName(agentId));
    }

    public async Task LeaveAgentGroupAsync(int agentId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetAgentGroupName(agentId));
    }

    public static string GetAgentGroupName(int agentId) => $"agent:{agentId}";
}

public sealed class AgentCommandRealtimeNotifier : IAgentCommandRealtimeNotifier
{
    private readonly IHubContext<AgentHub> _hubContext;
    private readonly ILogger<AgentCommandRealtimeNotifier> _logger;

    public AgentCommandRealtimeNotifier(IHubContext<AgentHub> hubContext, ILogger<AgentCommandRealtimeNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task CommandUpdatedAsync(AgentCommandDto command, CancellationToken cancellationToken)
    {
        try
        {
            await _hubContext.Clients
                .Group(AgentHub.GetAgentGroupName(command.AgentId))
                .SendAsync(AgentHub.EventName, command, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Agent command SignalR yayını başarısız: CommandId={CommandId}", command.Id);
        }
    }
}
