using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using STYS.Agent.Client;
using STYS.Agent.Client.Authentication;
using STYS.Agent.Client.Infrastructure;
using STYS.Agent.Configuration;
using STYS.Agent.Contracts.Dtos;
using STYS.Agent.Diagnostics;
using STYS.Agent.Services;

namespace STYS.Tests.Agent;

public sealed class AgentLocalManagementPhaseA3Tests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "stys-agent-a3-tests", Guid.NewGuid().ToString("N"));

    public AgentLocalManagementPhaseA3Tests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }
        catch
        {
        }
    }

    [Fact]
    public async Task SaveConfiguration_BaseUrlChanged_WhenCredentialPresent_RequiresReEnrollment_AndResetsAuth()
    {
        var resolver = new TempAgentPathResolver(_tempDir);
        var bootstrapStore = new FileAgentBootstrapConfigurationStore(resolver, NullLogger<FileAgentBootstrapConfigurationStore>.Instance);
        await bootstrapStore.SaveAsync(new AgentBootstrapConfiguration
        {
            StysBaseUrl = "https://old.example/stys/api",
            AgentDisplayName = "Agent",
            HttpTimeoutSeconds = 30,
            LocalUiPort = 5180
        }, CancellationToken.None);

        var credentialStore = new FileAgentCredentialStore(resolver, NullLogger<FileAgentCredentialStore>.Instance);
        await credentialStore.SaveAsync(new AgentLocalCredential
        {
            ClientId = "client",
            ClientSecret = "secret",
            AgentInstanceId = "instance",
            EnrollmentBaseUrl = "https://old.example/stys/api",
            AgentId = 1,
            CreatedAt = DateTime.UtcNow
        }, CancellationToken.None);

        var authState = new AgentAuthenticationState();
        authState.MarkAuthenticated();
        var tokenStore = new AgentTokenStore();
        tokenStore.SetToken(new AgentTokenResponse { AccessToken = "jwt", ExpiresAt = DateTime.UtcNow.AddMinutes(30) });
        var runtimeStatus = new AgentRuntimeStatus();
        var service = CreateService(resolver, bootstrapStore, credentialStore, authState, runtimeStatus, tokenStore);

        var result = await service.SaveConfigurationAsync(new AgentBootstrapConfiguration
        {
            StysBaseUrl = "https://new.example/stys/api",
            AgentDisplayName = "Agent",
            HttpTimeoutSeconds = 30,
            LocalUiPort = 5180
        }, CancellationToken.None);

        Assert.True(result.ReEnrollmentRequired);
        Assert.False(authState.IsReady);
        Assert.False(tokenStore.HasValidToken());
        Assert.True(runtimeStatus.RequiresReEnrollment);
        Assert.NotNull(await credentialStore.GetAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ResetEnrollment_ClearsCredentialTokenAndAuthState()
    {
        var resolver = new TempAgentPathResolver(_tempDir);
        var bootstrapStore = new FileAgentBootstrapConfigurationStore(resolver, NullLogger<FileAgentBootstrapConfigurationStore>.Instance);
        await bootstrapStore.SaveAsync(new AgentBootstrapConfiguration
        {
            StysBaseUrl = "https://example.org/stys/api",
            AgentDisplayName = "Agent",
            HttpTimeoutSeconds = 30,
            LocalUiPort = 5180
        }, CancellationToken.None);

        var credentialStore = new FileAgentCredentialStore(resolver, NullLogger<FileAgentCredentialStore>.Instance);
        await credentialStore.SaveAsync(new AgentLocalCredential
        {
            ClientId = "client",
            ClientSecret = "secret",
            AgentInstanceId = "instance",
            EnrollmentBaseUrl = "https://example.org/stys/api",
            AgentId = 1,
            CreatedAt = DateTime.UtcNow
        }, CancellationToken.None);

        var authState = new AgentAuthenticationState();
        authState.MarkAuthenticated();
        var tokenStore = new AgentTokenStore();
        tokenStore.SetToken(new AgentTokenResponse { AccessToken = "jwt", ExpiresAt = DateTime.UtcNow.AddMinutes(30) });
        var runtimeStatus = new AgentRuntimeStatus();
        runtimeStatus.MarkAuthenticated();
        var service = CreateService(resolver, bootstrapStore, credentialStore, authState, runtimeStatus, tokenStore);

        var result = await service.ResetEnrollmentAsync(new AgentBootstrapResetRequest
        {
            ConfirmationText = "Bu işlem yerel Agent kimlik bilgilerini silecek. Merkezi STYS kaydı silinmeyecektir. Agent yeniden enrollment gerektirecektir."
        }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(result.CredentialCleared);
        Assert.True(result.TokenCleared);
        Assert.True(result.AuthenticationReset);
        Assert.False(authState.IsReady);
        Assert.False(tokenStore.HasValidToken());
        Assert.Null(await credentialStore.GetAsync(CancellationToken.None));
    }

    [Fact]
    public void LogBuffer_MasksSecretLikeValues()
    {
        var buffer = new AgentInMemoryLogBuffer();
        buffer.Add("STYS.Agent.Services.AgentEnrollmentCoordinator", "Information", "clientsecret=super-secret token=jwt-token enrollmentcode=ABC12345", DateTimeOffset.UtcNow);

        var entry = buffer.GetRecent(1).Single();
        Assert.DoesNotContain("super-secret", entry.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("jwt-token", entry.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ABC12345", entry.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[REDACTED]", entry.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryActivate_RejectsCredentialForDifferentBaseUrl()
    {
        var resolver = new TempAgentPathResolver(_tempDir);
        var bootstrapStore = new FileAgentBootstrapConfigurationStore(resolver, NullLogger<FileAgentBootstrapConfigurationStore>.Instance);
        await bootstrapStore.SaveAsync(new AgentBootstrapConfiguration
        {
            StysBaseUrl = "https://new.example/stys/api",
            AgentDisplayName = "Agent",
            HttpTimeoutSeconds = 30,
            LocalUiPort = 5180
        }, CancellationToken.None);

        var credentialStore = new FileAgentCredentialStore(resolver, NullLogger<FileAgentCredentialStore>.Instance);
        await credentialStore.SaveAsync(new AgentLocalCredential
        {
            ClientId = "client",
            ClientSecret = "secret",
            AgentInstanceId = "instance",
            EnrollmentBaseUrl = "https://old.example/stys/api",
            AgentId = 1,
            CreatedAt = DateTime.UtcNow
        }, CancellationToken.None);

        var authState = new AgentAuthenticationState();
        var tokenStore = new AgentTokenStore();
        var runtimeStatus = new AgentRuntimeStatus();
        var client = new CountingClient();
        var coordinator = new AgentEnrollmentCoordinator(
            bootstrapStore,
            new SuccessConnectionTester(),
            credentialStore,
            client,
            tokenStore,
            authState,
            runtimeStatus,
            resolver,
            Options.Create(new StysAgentClientOptions
            {
                BaseUrl = "https://new.example/stys/api",
                RequestTimeoutSeconds = 30,
                AgentVersion = "1.0.0"
            }),
            NullLogger<AgentEnrollmentCoordinator>.Instance);

        var activated = await coordinator.TryActivateAsync(CancellationToken.None);

        Assert.False(activated);
        Assert.False(authState.IsReady);
        Assert.False(tokenStore.HasValidToken());
        Assert.True(runtimeStatus.RequiresReEnrollment);
        Assert.Equal(0, client.TokenCallCount);
    }

    private AgentBootstrapManagementService CreateService(
        IAgentPathResolver resolver,
        IAgentBootstrapConfigurationStore bootstrapStore,
        IAgentCredentialStore credentialStore,
        IAgentAuthenticationState authState,
        IAgentRuntimeStatus runtimeStatus,
        AgentTokenStore tokenStore)
    {
        return new AgentBootstrapManagementService(
            bootstrapStore,
            new SuccessConnectionTester(),
            credentialStore,
            runtimeStatus,
            authState,
            new AgentBootstrapConnectionTestState(),
            tokenStore,
            new AgentInMemoryLogBuffer(),
            new DummyClient(),
            resolver,
            Options.Create(new StysAgentClientOptions
            {
                BaseUrl = "https://example.org/stys/api",
                RequestTimeoutSeconds = 30,
                AgentVersion = "1.0.0"
            }));
    }

    private sealed class TempAgentPathResolver : IAgentPathResolver
    {
        public TempAgentPathResolver(string root) => DataDirectory = root;
        public string DataDirectory { get; }
        public string LogDirectory => Path.Combine(DataDirectory, "logs");
        public string BootstrapConfigurationPath => Path.Combine(DataDirectory, "bootstrap.json");
        public string CredentialStorePath => Path.Combine(DataDirectory, "credential.dat");
        public string LocalDevicesStorePath => Path.Combine(DataDirectory, "local-devices.json");
        public string LocalDeviceTerminalsStorePath => Path.Combine(DataDirectory, "local-device-terminals.json");
        public string PavoPairingStorePath => Path.Combine(DataDirectory, "pavo-pairing.dat");
        public string AgentCommandExecutionStorePath => Path.Combine(DataDirectory, "agent-command-executions.json");
        public string InstanceIdPath => Path.Combine(DataDirectory, "instance.id");
    }

    private sealed class SuccessConnectionTester : IAgentBootstrapConnectionTester
    {
        public Task<AgentBootstrapConnectionTestResult> TestAsync(string baseUrl, int timeoutSeconds, CancellationToken cancellationToken) =>
            Task.FromResult(new AgentBootstrapConnectionTestResult
            {
                Success = true,
                Status = "ok",
                Message = "Bağlantı başarılı."
            });
    }

    private sealed class DummyClient : IStysAgentApiClient
    {
        public Task<AgentEnrollmentResponse> EnrollAsync(AgentEnrollmentRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<AgentEnrollmentStatusResponse> GetEnrollmentStatusAsync(AgentEnrollmentStatusRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<AgentTokenResponse> GetTokenAsync(AgentTokenRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task SendHeartbeatAsync(AgentHeartbeatRequest request, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<AgentConfigDto?> GetConfigurationAsync(long currentVersion, CancellationToken cancellationToken) => Task.FromResult<AgentConfigDto?>(null);
        public Task<AgentSelfDto> GetMeAsync(CancellationToken cancellationToken) => Task.FromResult(new AgentSelfDto());
        public Task<IReadOnlyCollection<AgentCommandDto>> GetPendingCommandsAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<AgentCommandDto>>([]);
        public Task AcceptCommandAsync(Guid commandId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SetRunningCommandAsync(Guid commandId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task CompleteCommandAsync(Guid commandId, AgentCommandCompleteRequest request, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task FailCommandAsync(Guid commandId, AgentCommandCompleteRequest request, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task RejectCommandAsync(Guid commandId, AgentCommandCompleteRequest request, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class CountingClient : IStysAgentApiClient
    {
        public int TokenCallCount { get; private set; }
        public Task<AgentEnrollmentResponse> EnrollAsync(AgentEnrollmentRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<AgentEnrollmentStatusResponse> GetEnrollmentStatusAsync(AgentEnrollmentStatusRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<AgentTokenResponse> GetTokenAsync(AgentTokenRequest request, CancellationToken cancellationToken)
        {
            TokenCallCount++;
            return Task.FromResult(new AgentTokenResponse { AccessToken = "jwt", ExpiresAt = DateTime.UtcNow.AddMinutes(10) });
        }

        public Task SendHeartbeatAsync(AgentHeartbeatRequest request, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<AgentConfigDto?> GetConfigurationAsync(long currentVersion, CancellationToken cancellationToken) => Task.FromResult<AgentConfigDto?>(null);
        public Task<AgentSelfDto> GetMeAsync(CancellationToken cancellationToken) => Task.FromResult(new AgentSelfDto());
        public Task<IReadOnlyCollection<AgentCommandDto>> GetPendingCommandsAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<AgentCommandDto>>([]);
        public Task AcceptCommandAsync(Guid commandId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SetRunningCommandAsync(Guid commandId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task CompleteCommandAsync(Guid commandId, AgentCommandCompleteRequest request, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task FailCommandAsync(Guid commandId, AgentCommandCompleteRequest request, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task RejectCommandAsync(Guid commandId, AgentCommandCompleteRequest request, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
