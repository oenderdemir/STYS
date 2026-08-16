namespace STYS.Agent.Services;

public sealed class AgentRuntimeStatus : IAgentRuntimeStatus
{
    private readonly object _gate = new();
    private DateTimeOffset? _lastSuccessfulStysConnectionAt;
    private string? _lastStysConnectionError;
    private DateTimeOffset? _lastHeartbeatSuccessAt;
    private string? _lastHeartbeatError;
    private DateTimeOffset? _lastCommandPollSuccessAt;
    private string? _lastCommandPollError;
    private DateTimeOffset? _lastResetAt;
    private DateTimeOffset? _lastStartupValidationAt;
    private bool _startupHealthy = true;
    private string? _startupHealthError;
    private bool _credentialPresent;
    private bool _authenticationReady;
    private bool _pendingApproval;
    private bool _requiresReEnrollment;
    private string? _requiresReEnrollmentReason;

    public DateTimeOffset ProcessStartTime { get; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastSuccessfulStysConnectionAt
    {
        get { lock (_gate) return _lastSuccessfulStysConnectionAt; }
    }

    public string? LastStysConnectionError
    {
        get { lock (_gate) return _lastStysConnectionError; }
    }

    public DateTimeOffset? LastHeartbeatSuccessAt
    {
        get { lock (_gate) return _lastHeartbeatSuccessAt; }
    }

    public string? LastHeartbeatError
    {
        get { lock (_gate) return _lastHeartbeatError; }
    }

    public DateTimeOffset? LastCommandPollSuccessAt
    {
        get { lock (_gate) return _lastCommandPollSuccessAt; }
    }

    public string? LastCommandPollError
    {
        get { lock (_gate) return _lastCommandPollError; }
    }

    public DateTimeOffset? LastResetAt
    {
        get { lock (_gate) return _lastResetAt; }
    }

    public DateTimeOffset? LastStartupValidationAt
    {
        get { lock (_gate) return _lastStartupValidationAt; }
    }

    public bool StartupHealthy
    {
        get { lock (_gate) return _startupHealthy; }
    }

    public string? StartupHealthError
    {
        get { lock (_gate) return _startupHealthError; }
    }

    public bool CredentialPresent
    {
        get { lock (_gate) return _credentialPresent; }
    }

    public bool AuthenticationReady
    {
        get { lock (_gate) return _authenticationReady; }
    }

    public bool PendingApproval
    {
        get { lock (_gate) return _pendingApproval; }
    }

    public bool RequiresReEnrollment
    {
        get { lock (_gate) return _requiresReEnrollment; }
    }

    public string? RequiresReEnrollmentReason
    {
        get { lock (_gate) return _requiresReEnrollmentReason; }
    }

    public void MarkSuccessfulConnection()
    {
        lock (_gate)
        {
            _lastSuccessfulStysConnectionAt = DateTimeOffset.UtcNow;
            _lastStysConnectionError = null;
        }
    }

    public void MarkFailedConnection(string message)
    {
        lock (_gate)
        {
            _lastStysConnectionError = string.IsNullOrWhiteSpace(message) ? "STYS connection failed." : message.Trim();
        }
    }

    public void MarkHeartbeatSuccess()
    {
        lock (_gate)
        {
            _lastHeartbeatSuccessAt = DateTimeOffset.UtcNow;
            _lastHeartbeatError = null;
        }
    }

    public void MarkHeartbeatFailure(string message)
    {
        lock (_gate)
        {
            _lastHeartbeatError = string.IsNullOrWhiteSpace(message) ? "Heartbeat failed." : message.Trim();
        }
    }

    public void MarkCommandPollSuccess()
    {
        lock (_gate)
        {
            _lastCommandPollSuccessAt = DateTimeOffset.UtcNow;
            _lastCommandPollError = null;
        }
    }

    public void MarkCommandPollFailure(string message)
    {
        lock (_gate)
        {
            _lastCommandPollError = string.IsNullOrWhiteSpace(message) ? "Command polling failed." : message.Trim();
        }
    }

    public void MarkCredentialPresent(bool present)
    {
        lock (_gate)
        {
            _credentialPresent = present;
        }
    }

    public void MarkStartupHealthy()
    {
        lock (_gate)
        {
            _startupHealthy = true;
            _startupHealthError = null;
            _lastStartupValidationAt = DateTimeOffset.UtcNow;
        }
    }

    public void MarkStartupUnhealthy(string message)
    {
        lock (_gate)
        {
            _startupHealthy = false;
            _startupHealthError = string.IsNullOrWhiteSpace(message) ? "Agent startup validation failed." : message.Trim();
            _lastStartupValidationAt = DateTimeOffset.UtcNow;
        }
    }

    public void MarkAuthenticated()
    {
        lock (_gate)
        {
            _authenticationReady = true;
            _pendingApproval = false;
            _requiresReEnrollment = false;
            _requiresReEnrollmentReason = null;
        }
    }

    public void MarkPendingApproval()
    {
        lock (_gate)
        {
            // Registered and holding a credential, but not authorized yet: not an error state and
            // not a re-enrollment trigger.
            _pendingApproval = true;
            _authenticationReady = false;
            _requiresReEnrollment = false;
            _requiresReEnrollmentReason = null;
        }
    }

    public void ResetAuthentication()
    {
        lock (_gate)
        {
            _authenticationReady = false;
        }
    }

    public void MarkReEnrollmentRequired(string reason)
    {
        lock (_gate)
        {
            _requiresReEnrollment = true;
            _requiresReEnrollmentReason = string.IsNullOrWhiteSpace(reason) ? "Re-enrollment required." : reason.Trim();
        }
    }

    public void ClearReEnrollmentRequired()
    {
        lock (_gate)
        {
            _requiresReEnrollment = false;
            _requiresReEnrollmentReason = null;
        }
    }

    public void MarkReset()
    {
        lock (_gate)
        {
            _lastResetAt = DateTimeOffset.UtcNow;
            _authenticationReady = false;
            _credentialPresent = false;
            _requiresReEnrollment = false;
            _requiresReEnrollmentReason = null;
            _lastSuccessfulStysConnectionAt = null;
            _lastStysConnectionError = null;
            _lastHeartbeatError = null;
            _lastCommandPollError = null;
        }
    }
}
