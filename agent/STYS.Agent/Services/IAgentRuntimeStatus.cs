namespace STYS.Agent.Services;

public interface IAgentRuntimeStatus
{
    DateTimeOffset ProcessStartTime { get; }
    DateTimeOffset? LastSuccessfulStysConnectionAt { get; }
    string? LastStysConnectionError { get; }
    DateTimeOffset? LastHeartbeatSuccessAt { get; }
    string? LastHeartbeatError { get; }
    DateTimeOffset? LastCommandPollSuccessAt { get; }
    string? LastCommandPollError { get; }
    DateTimeOffset? LastResetAt { get; }
    DateTimeOffset? LastStartupValidationAt { get; }
    bool StartupHealthy { get; }
    string? StartupHealthError { get; }
    bool CredentialPresent { get; }
    bool AuthenticationReady { get; }
    bool RequiresReEnrollment { get; }
    string? RequiresReEnrollmentReason { get; }

    void MarkSuccessfulConnection();
    void MarkFailedConnection(string message);
    void MarkHeartbeatSuccess();
    void MarkHeartbeatFailure(string message);
    void MarkCommandPollSuccess();
    void MarkCommandPollFailure(string message);
    void MarkCredentialPresent(bool present);
    void MarkStartupHealthy();
    void MarkStartupUnhealthy(string message);
    void MarkAuthenticated();
    void ResetAuthentication();
    void MarkReEnrollmentRequired(string reason);
    void ClearReEnrollmentRequired();
    void MarkReset();
}
