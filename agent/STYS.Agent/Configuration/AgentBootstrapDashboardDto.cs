namespace STYS.Agent.Configuration;

public sealed class AgentBootstrapDashboardDto
{
    public string AgentDurumu { get; set; } = string.Empty;
    public string StysAdresi { get; set; } = string.Empty;
    public string EnrollmentDurumu { get; set; } = string.Empty;
    public string AgentDisplayName { get; set; } = string.Empty;
    public string AgentVersion { get; set; } = string.Empty;
    public string LocalUiVersion { get; set; } = string.Empty;
    public bool CredentialMevcutMu { get; set; }
    public string? StysServerVersion { get; set; }
    public string? StysConnectionDurumu { get; set; }
    public string? HeartbeatWorkerDurumu { get; set; }
    public string? CommandWorkerDurumu { get; set; }
    public string? ReEnrollmentNotu { get; set; }
    public AgentRuntimeSnapshotDto Runtime { get; set; } = new();
    public AgentBootstrapConnectionTestResult? SonBaglantiTesti { get; set; }
    public STYS.Agent.Contracts.Dtos.AgentSelfDto? Agent { get; set; }
}

public sealed class AgentRuntimeSnapshotDto
{
    public DateTimeOffset ProcessStartTimeUtc { get; set; }
    public DateTimeOffset? LastSuccessfulStysConnectionAt { get; set; }
    public string? LastStysConnectionError { get; set; }
    public DateTimeOffset? LastHeartbeatSuccessAt { get; set; }
    public string? LastHeartbeatError { get; set; }
    public DateTimeOffset? LastCommandPollSuccessAt { get; set; }
    public string? LastCommandPollError { get; set; }
    public DateTimeOffset? LastResetAt { get; set; }
    public bool CredentialPresent { get; set; }
    public bool AuthenticationReady { get; set; }
    public bool RequiresReEnrollment { get; set; }
    public string? RequiresReEnrollmentReason { get; set; }
}
