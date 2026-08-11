using System.Reflection;
using Microsoft.Extensions.Options;
using STYS.Agent.Client;
using STYS.Agent.Client.Authentication;
using STYS.Agent.Client.Infrastructure;
using STYS.Agent.Contracts.Dtos;
using STYS.Agent.Diagnostics;
using STYS.Agent.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Agent.Configuration;

public sealed class AgentBootstrapManagementService : IAgentBootstrapManagementService
{
    private const string ResetConfirmationText = "Bu işlem yerel Agent kimlik bilgilerini silecek. Merkezi STYS kaydı silinmeyecektir. Agent yeniden enrollment gerektirecektir.";

    private readonly IAgentBootstrapConfigurationStore _store;
    private readonly IAgentBootstrapConnectionTester _connectionTester;
    private readonly IAgentCredentialStore _credentialStore;
    private readonly IAgentRuntimeStatus _runtimeStatus;
    private readonly IAgentAuthenticationState _authenticationState;
    private readonly AgentBootstrapConnectionTestState _testState;
    private readonly AgentTokenStore _tokenStore;
    private readonly IAgentLogBuffer _logBuffer;
    private readonly IStysAgentApiClient _client;
    private readonly IAgentPathResolver _paths;
    private readonly StysAgentClientOptions _clientOptions;

    public AgentBootstrapManagementService(
        IAgentBootstrapConfigurationStore store,
        IAgentBootstrapConnectionTester connectionTester,
        IAgentCredentialStore credentialStore,
        IAgentRuntimeStatus runtimeStatus,
        IAgentAuthenticationState authenticationState,
        AgentBootstrapConnectionTestState testState,
        AgentTokenStore tokenStore,
        IAgentLogBuffer logBuffer,
        IStysAgentApiClient client,
        IAgentPathResolver paths,
        IOptions<StysAgentClientOptions> clientOptions)
    {
        _store = store;
        _connectionTester = connectionTester;
        _credentialStore = credentialStore;
        _runtimeStatus = runtimeStatus;
        _authenticationState = authenticationState;
        _testState = testState;
        _tokenStore = tokenStore;
        _logBuffer = logBuffer;
        _client = client;
        _paths = paths;
        _clientOptions = clientOptions.Value;
    }

    public Task<AgentBootstrapConfiguration> GetConfigurationAsync(CancellationToken cancellationToken) =>
        _store.GetAsync(cancellationToken);

    public async Task<AgentBootstrapConfigurationSaveResult> SaveConfigurationAsync(AgentBootstrapConfiguration configuration, CancellationToken cancellationToken)
    {
        Validate(configuration);

        var current = await _store.GetAsync(cancellationToken);
        var baseUrlChanged = !string.Equals(NormalizeBaseUrl(current.StysBaseUrl), NormalizeBaseUrl(configuration.StysBaseUrl), StringComparison.OrdinalIgnoreCase);
        var restartRequired = current.LocalUiPort != configuration.LocalUiPort;

        await _store.SaveAsync(configuration, cancellationToken);
        _clientOptions.BaseUrl = NormalizeBaseUrl(configuration.StysBaseUrl);
        _clientOptions.RequestTimeoutSeconds = configuration.HttpTimeoutSeconds;
        _clientOptions.EnrollmentCode = null;

        var credential = await _credentialStore.GetAsync(cancellationToken);
        var reEnrollmentRequired = false;

        if (credential is not null)
        {
            _runtimeStatus.MarkCredentialPresent(true);

            if (baseUrlChanged || !CredentialMatchesCurrentBaseUrl(credential, _clientOptions.BaseUrl))
            {
                _tokenStore.ClearToken();
                _authenticationState.Reset();
                _clientOptions.ClientId = string.Empty;
                _clientOptions.ClientSecret = string.Empty;
                _clientOptions.AgentInstanceId = string.Empty;
                _runtimeStatus.ResetAuthentication();
                _runtimeStatus.MarkReEnrollmentRequired("STYS adresi değiştiği için mevcut local credential yeniden kullanılamaz.");
                reEnrollmentRequired = true;
            }
        }

        return new AgentBootstrapConfigurationSaveResult
        {
            Configuration = await _store.GetAsync(cancellationToken),
            RestartRequired = restartRequired,
            ReEnrollmentRequired = reEnrollmentRequired,
            Message = BuildConfigurationSaveMessage(restartRequired, reEnrollmentRequired)
        };
    }

    public async Task<AgentBootstrapDashboardDto> GetDashboardAsync(CancellationToken cancellationToken)
    {
        var configuration = await _store.GetAsync(cancellationToken);
        var credential = await _credentialStore.GetAsync(cancellationToken);
        var connectionTest = _testState.LastResult;
        var reEnrollmentRequired = credential is not null && !CredentialMatchesCurrentBaseUrl(credential, configuration.StysBaseUrl);
        if (reEnrollmentRequired)
        {
            _runtimeStatus.MarkReEnrollmentRequired("STYS adresi değiştiği için mevcut local credential yeniden kullanılamaz.");
        }

        var self = await TryGetAgentSelfAsync(cancellationToken);
        var runtime = SnapshotRuntime(credential is not null);

        return new AgentBootstrapDashboardDto
        {
            AgentDurumu = ResolveAgentStatus(credential, self, runtime),
            StysAdresi = configuration.StysBaseUrl,
            EnrollmentDurumu = ResolveEnrollmentStatus(credential, reEnrollmentRequired),
            AgentDisplayName = configuration.AgentDisplayName,
            AgentVersion = ResolveAgentVersion(),
            LocalUiVersion = ResolveLocalUiVersion(),
            CredentialMevcutMu = credential is not null,
            StysServerVersion = connectionTest?.Version,
            StysConnectionDurumu = ResolveConnectionStatus(connectionTest, runtime, reEnrollmentRequired),
            HeartbeatWorkerDurumu = ResolveWorkerStatus(runtime.AuthenticationReady, runtime.LastHeartbeatSuccessAt, runtime.LastHeartbeatError),
            CommandWorkerDurumu = ResolveWorkerStatus(runtime.AuthenticationReady, runtime.LastCommandPollSuccessAt, runtime.LastCommandPollError),
            ReEnrollmentNotu = runtime.RequiresReEnrollmentReason,
            Runtime = runtime,
            SonBaglantiTesti = connectionTest,
            Agent = self
        };
    }

    public async Task<AgentBootstrapDiagnosticsDto> GetDiagnosticsAsync(CancellationToken cancellationToken)
    {
        var configuration = await _store.GetAsync(cancellationToken);
        var credential = await _credentialStore.GetAsync(cancellationToken);
        var reEnrollmentRequired = credential is not null && !CredentialMatchesCurrentBaseUrl(credential, configuration.StysBaseUrl);
        if (reEnrollmentRequired)
        {
            _runtimeStatus.MarkReEnrollmentRequired("STYS adresi değiştiği için mevcut local credential yeniden kullanılamaz.");
        }

        var runtime = SnapshotRuntime(credential is not null);

        return new AgentBootstrapDiagnosticsDto
        {
            AgentVersion = ResolveAgentVersion(),
            LocalUiVersion = ResolveLocalUiVersion(),
            ProcessId = Environment.ProcessId.ToString(),
            ProcessStartTimeUtc = runtime.ProcessStartTimeUtc,
            Uptime = FormatUptime(DateTimeOffset.UtcNow - runtime.ProcessStartTimeUtc),
            MachineName = Environment.MachineName,
            OperatingSystem = Environment.OSVersion.VersionString,
            FrameworkDescription = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            DataDirectory = _paths.DataDirectory,
            BootstrapConfigurationPath = _paths.BootstrapConfigurationPath,
            CredentialStorePath = _paths.CredentialStorePath,
            StysBaseUrl = configuration.StysBaseUrl,
            CredentialPresent = credential is not null,
            AuthenticationReady = runtime.AuthenticationReady,
            RequiresReEnrollment = runtime.RequiresReEnrollment,
            RequiresReEnrollmentReason = runtime.RequiresReEnrollmentReason,
            LastSuccessfulStysConnectionAt = runtime.LastSuccessfulStysConnectionAt,
            LastHeartbeatSuccessAt = runtime.LastHeartbeatSuccessAt,
            LastHeartbeatError = runtime.LastHeartbeatError,
            LastCommandPollSuccessAt = runtime.LastCommandPollSuccessAt,
            LastCommandPollError = runtime.LastCommandPollError,
            LastResetAt = runtime.LastResetAt,
            RecentLogs = _logBuffer.GetRecent(100)
        };
    }

    public async Task<AgentBootstrapResetResult> ResetEnrollmentAsync(AgentBootstrapResetRequest request, CancellationToken cancellationToken)
    {
        if (!string.Equals(request.ConfirmationText?.Trim(), ResetConfirmationText, StringComparison.Ordinal))
        {
            return new AgentBootstrapResetResult
            {
                Success = false,
                Message = "Onay metni eşleşmedi."
            };
        }

        await _credentialStore.DeleteAsync(cancellationToken);
        _tokenStore.ClearToken();
        _authenticationState.Reset();
        _clientOptions.ClientId = string.Empty;
        _clientOptions.ClientSecret = string.Empty;
        _clientOptions.AgentInstanceId = string.Empty;
        _runtimeStatus.MarkReset();

        try
        {
            Environment.SetEnvironmentVariable("STYS_ENROLLMENT_CODE", null, EnvironmentVariableTarget.Process);
        }
        catch
        {
        }

        return new AgentBootstrapResetResult
        {
            Success = true,
            Message = "Yerel enrollment bilgileri sıfırlandı. Agent yeniden enrollment bekliyor.",
            CredentialCleared = true,
            TokenCleared = true,
            AuthenticationReset = true
        };
    }

    public async Task<AgentBootstrapConnectionTestResult> TestConnectionAsync(AgentBootstrapConfiguration configuration, CancellationToken cancellationToken)
    {
        Validate(configuration);
        var result = await _connectionTester.TestAsync(configuration.StysBaseUrl, configuration.HttpTimeoutSeconds, cancellationToken);
        _testState.LastResult = result;

        if (result.Success)
            _runtimeStatus.MarkSuccessfulConnection();
        else
            _runtimeStatus.MarkFailedConnection(result.Message);

        return result;
    }

    private AgentRuntimeSnapshotDto SnapshotRuntime(bool credentialPresent)
    {
        return new AgentRuntimeSnapshotDto
        {
            ProcessStartTimeUtc = _runtimeStatus.ProcessStartTime,
            LastSuccessfulStysConnectionAt = _runtimeStatus.LastSuccessfulStysConnectionAt,
            LastHeartbeatSuccessAt = _runtimeStatus.LastHeartbeatSuccessAt,
            LastHeartbeatError = _runtimeStatus.LastHeartbeatError,
            LastCommandPollSuccessAt = _runtimeStatus.LastCommandPollSuccessAt,
            LastCommandPollError = _runtimeStatus.LastCommandPollError,
            LastResetAt = _runtimeStatus.LastResetAt,
            CredentialPresent = credentialPresent,
            AuthenticationReady = _authenticationState.IsReady,
            RequiresReEnrollment = _runtimeStatus.RequiresReEnrollment,
            RequiresReEnrollmentReason = _runtimeStatus.RequiresReEnrollmentReason
        };
    }

    private async Task<AgentSelfDto?> TryGetAgentSelfAsync(CancellationToken cancellationToken)
    {
        if (!_authenticationState.IsReady)
            return null;

        try
        {
            return await _client.GetMeAsync(cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private static string ResolveAgentVersion() =>
        Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";

    private static string ResolveLocalUiVersion() =>
        typeof(AgentBootstrapManagementService).Assembly.GetName().Version?.ToString() ?? "unknown";

    private static string ResolveAgentStatus(AgentLocalCredential? credential, AgentSelfDto? self, AgentRuntimeSnapshotDto runtime)
    {
        if (credential is null)
            return "Kayıtlı değil";

        if (runtime.RequiresReEnrollment)
            return "Yeniden enrollment gerekli";

        if (!runtime.AuthenticationReady)
            return "Kimlik doğrulanıyor";

        return self?.OnlineMi == true ? "Online" : "Kimlik doğrulandı";
    }

    private static string ResolveEnrollmentStatus(AgentLocalCredential? credential, bool reEnrollmentRequired)
    {
        if (credential is null)
            return "Kayıtlı değil";

        return reEnrollmentRequired ? "Yeniden enrollment gerekli" : "Kayıtlı";
    }

    private static string ResolveConnectionStatus(AgentBootstrapConnectionTestResult? test, AgentRuntimeSnapshotDto runtime, bool reEnrollmentRequired)
    {
        if (reEnrollmentRequired)
            return "Yeniden enrollment gerekli";

        if (test is null)
            return runtime.LastSuccessfulStysConnectionAt is null ? "Bağlantı bekleniyor" : "Bağlı";

        if (test.Success)
            return "Bağlı";

        return test.Status switch
        {
            "timeout" => "STYS erişilemiyor",
            "tls-error" => "TLS hatası",
            "dns-error" => "DNS hatası",
            _ => "Bağlantı hatası"
        };
    }

    private static string ResolveWorkerStatus(bool authenticationReady, DateTimeOffset? lastSuccess, string? lastError)
    {
        if (!authenticationReady)
            return "Beklemede";

        if (!string.IsNullOrWhiteSpace(lastError))
            return $"Son hata: {lastError}";

        if (lastSuccess is null)
            return "Çalışıyor";

        var age = DateTimeOffset.UtcNow - lastSuccess.Value;
        return age < TimeSpan.FromMinutes(2) ? "Çalışıyor" : "Beklemede";
    }

    private static string FormatUptime(TimeSpan span)
    {
        if (span < TimeSpan.Zero)
            span = TimeSpan.Zero;

        return $"{(int)span.TotalDays}g {span.Hours:00}:{span.Minutes:00}:{span.Seconds:00}";
    }

    private static string BuildConfigurationSaveMessage(bool restartRequired, bool reEnrollmentRequired)
    {
        if (restartRequired && reEnrollmentRequired)
            return "Kaydedildi. Local UI portu için yeniden başlatma, STYS adresi değiştiği için yeniden enrollment gerekli.";

        if (restartRequired)
            return "Kaydedildi. Local UI portu için yeniden başlatma gerekli.";

        if (reEnrollmentRequired)
            return "Kaydedildi. STYS adresi değiştiği için yeniden enrollment gerekli.";

        return "Kaydedildi.";
    }

    private static string NormalizeBaseUrl(string? baseUrl)
    {
        var value = string.IsNullOrWhiteSpace(baseUrl) ? "https://localhost:7160" : baseUrl.Trim();
        return value.TrimEnd('/');
    }

    private static bool CredentialMatchesCurrentBaseUrl(AgentLocalCredential credential, string currentBaseUrl) =>
        string.Equals(NormalizeBaseUrl(credential.EnrollmentBaseUrl), NormalizeBaseUrl(currentBaseUrl), StringComparison.OrdinalIgnoreCase);

    private static void Validate(AgentBootstrapConfiguration configuration)
    {
        if (!Uri.TryCreate(configuration.StysBaseUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("Geçersiz STYS adresi.");
        }

        if (configuration.LocalUiPort <= 0 || configuration.LocalUiPort > 65535)
            throw new ArgumentOutOfRangeException(nameof(configuration.LocalUiPort));

        if (configuration.HttpTimeoutSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(configuration.HttpTimeoutSeconds));
    }
}

public sealed class AgentBootstrapConnectionTestState
{
    public AgentBootstrapConnectionTestResult? LastResult { get; set; }
}
