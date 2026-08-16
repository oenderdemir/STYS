namespace STYS.Agent.Contracts.Enums;

public enum AgentDurum
{
    PendingApproval = 0,
    Active = 1,
    Disabled = 2,
    Revoked = 3,
    /// <summary>Enrollment was explicitly refused by an operator. Distinct from Revoked, which
    /// withdraws access from an agent that was previously approved.</summary>
    Rejected = 4
}
