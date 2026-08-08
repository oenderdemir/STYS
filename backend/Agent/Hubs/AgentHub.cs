using Microsoft.AspNetCore.SignalR;
using STYS.Agent.Contracts.Dtos;
using STYS.Agent.Services;
using Microsoft.Extensions.Logging;

namespace STYS.Agent.Hubs;

public sealed class AgentHub : Hub
{
    public const string HubRoute = "/ui/agent-hub";
    public const string EventName = "AgentCommandUpdated";

    public async Task JoinAgentGroupAsync(int agentId)
    {
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
