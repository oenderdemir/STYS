using STYS.Agent.Contracts.Enums;

namespace STYS.Agent.Services;

public static class AgentCommandStateMachine
{
    private static readonly HashSet<AgentCommandStatus> CanTransitionTo = new()
    {
        AgentCommandStatus.Pending, AgentCommandStatus.Delivered, AgentCommandStatus.Accepted,
        AgentCommandStatus.Running, AgentCommandStatus.Completed, AgentCommandStatus.Failed,
        AgentCommandStatus.Cancelled, AgentCommandStatus.Expired, AgentCommandStatus.Rejected
    };

    private static readonly Dictionary<AgentCommandStatus, HashSet<AgentCommandStatus>> ValidTransitions = new()
    {
        [AgentCommandStatus.Pending] = new() { AgentCommandStatus.Delivered, AgentCommandStatus.Cancelled, AgentCommandStatus.Expired, AgentCommandStatus.Rejected },
        [AgentCommandStatus.Delivered] = new() { AgentCommandStatus.Accepted, AgentCommandStatus.Cancelled, AgentCommandStatus.Expired },
        [AgentCommandStatus.Accepted] = new() { AgentCommandStatus.Running, AgentCommandStatus.Failed, AgentCommandStatus.Cancelled, AgentCommandStatus.Expired },
        [AgentCommandStatus.Running] = new() { AgentCommandStatus.Completed, AgentCommandStatus.Failed, AgentCommandStatus.Cancelled },
        [AgentCommandStatus.Completed] = new() { },
        [AgentCommandStatus.Failed] = new() { },
        [AgentCommandStatus.Cancelled] = new() { },
        [AgentCommandStatus.Expired] = new() { },
        [AgentCommandStatus.Rejected] = new() { },
    };

    public static bool IsValidTransition(AgentCommandStatus from, AgentCommandStatus to)
    {
        return ValidTransitions.TryGetValue(from, out var allowed) && allowed.Contains(to);
    }

    public static void EnforceTransition(AgentCommandStatus from, AgentCommandStatus to, Guid commandId)
    {
        if (!IsValidTransition(from, to))
            throw new InvalidOperationException(
                $"Geçersiz durum geçişi: {from} → {to}. CommandId: {commandId}");
    }
}
