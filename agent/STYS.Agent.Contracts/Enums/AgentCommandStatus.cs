namespace STYS.Agent.Contracts.Enums;

public enum AgentCommandStatus
{
    Pending = 0,
    Delivered = 1,
    Accepted = 2,
    Running = 3,
    Completed = 4,
    Failed = 5,
    Cancelled = 6,
    Expired = 7,
    Rejected = 8
}
