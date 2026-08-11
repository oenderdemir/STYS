using System.Reflection;
using STYS.Agent.Client.Authentication;
using STYS.Agent.Services;

namespace STYS.Agent.Configuration;

public sealed class AgentBootstrapManagementService : IAgentBootstrapManagementService
{
    private readonly IAgentBootstrapConfigurationStore _store;
    private readonly IAgentBootstrapConnectionTester _connectionTester;
    private readonly IAgentCredentialStore _credentialStore;
    private readonly IAgentAuthenticationState _authenticationState;
    private readonly AgentBootstrapConnectionTestState _testState;

    public AgentBootstrapManagementService(
        IAgentBootstrapConfigurationStore store,
        IAgentBootstrapConnectionTester connectionTester,
        IAgentCredentialStore credentialStore,
        IAgentAuthenticationState authenticationState,
        AgentBootstrapConnectionTestState testState)
    {
        _store = store;
        _connectionTester = connectionTester;
        _credentialStore = credentialStore;
        _authenticationState = authenticationState;
        _testState = testState;
    }

    public Task<AgentBootstrapConfiguration> GetConfigurationAsync(CancellationToken cancellationToken) =>
        _store.GetAsync(cancellationToken);

    public async Task<AgentBootstrapConfiguration> SaveConfigurationAsync(AgentBootstrapConfiguration configuration, CancellationToken cancellationToken)
    {
        Validate(configuration);
        await _store.SaveAsync(configuration, cancellationToken);
        return await _store.GetAsync(cancellationToken);
    }

    public async Task<AgentBootstrapDashboardDto> GetDashboardAsync(CancellationToken cancellationToken)
    {
        var configuration = await _store.GetAsync(cancellationToken);
        var credential = await _credentialStore.GetAsync(cancellationToken);
        return new AgentBootstrapDashboardDto
        {
            AgentDurumu = _authenticationState.IsReady ? "Kimlik doğrulandı" : "Başlatıldı",
            StysAdresi = configuration.StysBaseUrl,
            EnrollmentDurumu = credential is null ? "Kayıtlı değil" : "Kayıtlı",
            AgentDisplayName = configuration.AgentDisplayName,
            AgentVersion = ResolveAgentVersion(),
            LocalUiVersion = ResolveLocalUiVersion(),
            CredentialMevcutMu = credential is not null,
            SonBaglantiTesti = _testState.LastResult
        };
    }

    public async Task<AgentBootstrapConnectionTestResult> TestConnectionAsync(AgentBootstrapConfiguration configuration, CancellationToken cancellationToken)
    {
        Validate(configuration);
        var result = await _connectionTester.TestAsync(configuration.StysBaseUrl, configuration.HttpTimeoutSeconds, cancellationToken);
        _testState.LastResult = result;
        return result;
    }

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

    private static string ResolveAgentVersion() =>
        Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";

    private static string ResolveLocalUiVersion() =>
        typeof(AgentBootstrapManagementService).Assembly.GetName().Version?.ToString() ?? "unknown";
}

public sealed class AgentBootstrapConnectionTestState
{
    public AgentBootstrapConnectionTestResult? LastResult { get; set; }
}
