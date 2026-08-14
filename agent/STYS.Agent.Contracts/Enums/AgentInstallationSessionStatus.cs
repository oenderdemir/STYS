namespace STYS.Agent.Contracts.Enums;

public enum AgentInstallationSessionStatus
{
    Created = 0,
    PackageReady = 1,
    Downloaded = 2,
    EnrollmentPending = 3,
    PendingApproval = 4,
    Enrolled = 5,
    Online = 6,
    Completed = 7,
    Expired = 8,
    Cancelled = 9,
    Failed = 10
}
