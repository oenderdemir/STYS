using STYS.Agent.Diagnostics;

namespace STYS.Agent.Configuration;

public sealed class AgentBootstrapDiagnosticsDto
{
    public string AgentVersion { get; set; } = string.Empty;
    public string LocalUiVersion { get; set; } = string.Empty;
    public string ProcessId { get; set; } = string.Empty;
    public DateTimeOffset ProcessStartTimeUtc { get; set; }
    public string Uptime { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public string OperatingSystem { get; set; } = string.Empty;
    public string FrameworkDescription { get; set; } = string.Empty;
    public string DataDirectory { get; set; } = string.Empty;
    public string BootstrapConfigurationPath { get; set; } = string.Empty;
    public string StysBaseUrl { get; set; } = string.Empty;
    public bool CredentialPresent { get; set; }
    public bool AuthenticationReady { get; set; }
    public bool RequiresReEnrollment { get; set; }
    public string? RequiresReEnrollmentReason { get; set; }
    public DateTimeOffset? LastSuccessfulStysConnectionAt { get; set; }
    public string? LastStysConnectionError { get; set; }
    public DateTimeOffset? LastHeartbeatSuccessAt { get; set; }
    public string? LastHeartbeatError { get; set; }
    public DateTimeOffset? LastCommandPollSuccessAt { get; set; }
    public string? LastCommandPollError { get; set; }
    public DateTimeOffset? LastResetAt { get; set; }
    public IReadOnlyCollection<AgentLogEntryDto> RecentLogs { get; set; } = [];
}
