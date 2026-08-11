using System.Net;
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

public sealed class AgentLocalEnrollmentWizardPhaseATests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "stys-agent-a2-tests", Guid.NewGuid().ToString("N"));

    public AgentLocalEnrollmentWizardPhaseATests()
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
    public async Task ValidEnrollment_SavesCredential_ActivatesAuth_AndDoesNotPersistCode()
    {
        var resolver = new TempAgentPathResolver(_tempDir);
        var bootstrapStore = new FileAgentBootstrapConfigurationStore(resolver, NullLogger<FileAgentBootstrapConfigurationStore>.Instance);
        await bootstrapStore.SaveAsync(new AgentBootstrapConfiguration
        {
            StysBaseUrl = "https://example.org/stys/api",
            AgentDisplayName = "Resepsiyon Agent",
            HttpTimeoutSeconds = 30,
            LocalUiPort = 5180
        }, CancellationToken.None);

        var credentialStore = new FileAgentCredentialStore(resolver, NullLogger<FileAgentCredentialStore>.Instance);
        var authState = new AgentAuthenticationState();
        var tokenStore = new AgentTokenStore();
        var runtimeStatus = new AgentRuntimeStatus();
        var client = new RecordingAgentApiClient
        {
            EnrollResponse = new AgentEnrollmentResponse
            {
                AgentId = 42,
                ClientId = "client-42",
                ClientSecret = "super-secret",
                AgentKey = Environment.MachineName,
                Message = "✓ STYS'e kayıt başarılı"
            },
            TokenResponse = new AgentTokenResponse
            {
                AccessToken = "jwt-token",
                ExpiresAt = DateTime.UtcNow.AddMinutes(30)
            },
            SelfResponse = new AgentSelfDto
            {
                AgentId = 42,
                AgentAd = "Resepsiyon Agent",
                AgentKey = Environment.MachineName,
                KurumId = 7,
                KurumAd = "TRT",
                Tesisler = [new AgentSelfTesisDto { Id = 11, Ad = "Ana Tesis" }],
                Scopes = ["agent.heartbeat", "agent.command.read"],
                Capabilities = ["pavo"],
                Durum = 1,
                AgentVersion = "1.0.0",
                LastHeartbeatAt = DateTime.UtcNow,
                OnlineMi = true
            }
        };

        var coordinator = CreateCoordinator(resolver, bootstrapStore, credentialStore, client, tokenStore, authState, runtimeStatus);

        var result = await coordinator.EnrollAsync(new AgentBootstrapEnrollmentRequest
        {
            StysBaseUrl = "https://example.org/stys/api",
            AgentDisplayName = "Resepsiyon Agent",
            EnrollmentCode = "ABC12345",
            HttpTimeoutSeconds = 30,
            LocalUiPort = 5180
        }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(result.CredentialSaved);
        Assert.True(result.TokenAcquired);
        Assert.True(authState.IsReady);
        Assert.True(tokenStore.HasValidToken());
        Assert.Equal(1, client.EnrollCallCount);
        Assert.Equal(1, client.TokenCallCount);

        var storedCredential = await credentialStore.GetAsync(CancellationToken.None);
        Assert.NotNull(storedCredential);
        Assert.Equal("client-42", storedCredential!.ClientId);
        Assert.Equal(42, storedCredential.AgentId);
        Assert.Equal("super-secret", storedCredential.ClientSecret);

        var bootstrapJson = await File.ReadAllTextAsync(resolver.BootstrapConfigurationPath);
        Assert.DoesNotContain("ABC12345", bootstrapJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("super-secret", bootstrapJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ClientSecret", bootstrapJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConnectionFailure_BlocksEnrollment()
    {
        var resolver = new TempAgentPathResolver(_tempDir);
        var bootstrapStore = new FileAgentBootstrapConfigurationStore(resolver, NullLogger<FileAgentBootstrapConfigurationStore>.Instance);
        await bootstrapStore.SaveAsync(new AgentBootstrapConfiguration
        {
            StysBaseUrl = "https://example.org/stys/api",
            AgentDisplayName = "A",
            HttpTimeoutSeconds = 30,
            LocalUiPort = 5180
        }, CancellationToken.None);

        var credentialStore = new FileAgentCredentialStore(resolver, NullLogger<FileAgentCredentialStore>.Instance);
        var authState = new AgentAuthenticationState();
        var tokenStore = new AgentTokenStore();
        var runtimeStatus = new AgentRuntimeStatus();
        var client = new RecordingAgentApiClient();
        var coordinator = CreateCoordinator(resolver, bootstrapStore, credentialStore, client, tokenStore, authState, runtimeStatus, connectionSuccess: false);

        var result = await coordinator.EnrollAsync(new AgentBootstrapEnrollmentRequest
        {
            StysBaseUrl = "https://example.org/stys/api",
            AgentDisplayName = "A",
            EnrollmentCode = "ABC12345",
            HttpTimeoutSeconds = 30,
            LocalUiPort = 5180
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("STYS", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, client.EnrollCallCount);
        Assert.Null(await credentialStore.GetAsync(CancellationToken.None));
        Assert.False(authState.IsReady);
    }

    [Fact]
    public async Task ConcurrentEnrollment_OnlyOneRequestHitsServer()
    {
        var resolver = new TempAgentPathResolver(_tempDir);
        var bootstrapStore = new FileAgentBootstrapConfigurationStore(resolver, NullLogger<FileAgentBootstrapConfigurationStore>.Instance);
        await bootstrapStore.SaveAsync(new AgentBootstrapConfiguration
        {
            StysBaseUrl = "https://example.org/stys/api",
            AgentDisplayName = "A",
            HttpTimeoutSeconds = 30,
            LocalUiPort = 5180
        }, CancellationToken.None);

        var credentialStore = new FileAgentCredentialStore(resolver, NullLogger<FileAgentCredentialStore>.Instance);
        var authState = new AgentAuthenticationState();
        var tokenStore = new AgentTokenStore();
        var runtimeStatus = new AgentRuntimeStatus();
        var client = new RecordingAgentApiClient
        {
            EnrollResponse = new AgentEnrollmentResponse
            {
                AgentId = 100,
                ClientId = "client-100",
                ClientSecret = "secret-100",
                AgentKey = Environment.MachineName,
                Message = "ok"
            },
            TokenResponse = new AgentTokenResponse
            {
                AccessToken = "jwt-token-100",
                ExpiresAt = DateTime.UtcNow.AddMinutes(30)
            }
        };
        var coordinator = CreateCoordinator(resolver, bootstrapStore, credentialStore, client, tokenStore, authState, runtimeStatus);
        var request = new AgentBootstrapEnrollmentRequest
        {
            StysBaseUrl = "https://example.org/stys/api",
            AgentDisplayName = "A",
            EnrollmentCode = "ABC12345",
            HttpTimeoutSeconds = 30,
            LocalUiPort = 5180
        };

        await Task.WhenAll(
            coordinator.EnrollAsync(request, CancellationToken.None),
            coordinator.EnrollAsync(request, CancellationToken.None));

        Assert.Equal(1, client.EnrollCallCount);
        Assert.True(authState.IsReady);
    }

    [Fact]
    public async Task Dashboard_IncludesCentralProfile_WhenAuthenticated()
    {
        var resolver = new TempAgentPathResolver(_tempDir);
        var bootstrapStore = new FileAgentBootstrapConfigurationStore(resolver, NullLogger<FileAgentBootstrapConfigurationStore>.Instance);
        await bootstrapStore.SaveAsync(new AgentBootstrapConfiguration
        {
            StysBaseUrl = "https://example.org/stys/api",
            AgentDisplayName = "A",
            HttpTimeoutSeconds = 30,
            LocalUiPort = 5180
        }, CancellationToken.None);

        var credentialStore = new FileAgentCredentialStore(resolver, NullLogger<FileAgentCredentialStore>.Instance);
        await credentialStore.SaveAsync(new AgentLocalCredential
        {
            ClientId = "client-1",
            ClientSecret = "secret-1",
            AgentInstanceId = "instance-1",
            EnrollmentBaseUrl = "https://example.org/stys/api",
            AgentId = 11,
            CreatedAt = DateTime.UtcNow
        }, CancellationToken.None);

        var authState = new AgentAuthenticationState();
        authState.MarkAuthenticated();
        var tokenStore = new AgentTokenStore();
        var runtimeStatus = new AgentRuntimeStatus();
        tokenStore.SetToken(new AgentTokenResponse { AccessToken = "jwt", ExpiresAt = DateTime.UtcNow.AddMinutes(10) });
        var client = new RecordingAgentApiClient
        {
            SelfResponse = new AgentSelfDto
            {
                AgentId = 11,
                AgentAd = "A",
                KurumId = 7,
                KurumAd = "TRT",
                Tesisler = [new AgentSelfTesisDto { Id = 1, Ad = "Merkez" }],
                Scopes = ["agent.heartbeat"],
                Capabilities = ["pavo"],
                Durum = 1,
                AgentVersion = "1.0.0",
                LastHeartbeatAt = DateTime.UtcNow,
                OnlineMi = true
            }
        };

        var service = new AgentBootstrapManagementService(
            bootstrapStore,
            new SuccessConnectionTester(),
            credentialStore,
            runtimeStatus,
            authState,
            new AgentBootstrapConnectionTestState(),
            tokenStore,
            new AgentInMemoryLogBuffer(),
            client,
            resolver,
            Options.Create(new StysAgentClientOptions
            {
                BaseUrl = "https://example.org/stys/api",
                RequestTimeoutSeconds = 30,
                AgentVersion = "1.0.0"
            }));

        var dashboard = await service.GetDashboardAsync(CancellationToken.None);

        Assert.True(dashboard.CredentialMevcutMu);
        Assert.NotNull(dashboard.Agent);
        Assert.Equal("TRT", dashboard.Agent!.KurumAd);
        Assert.True(dashboard.Agent.OnlineMi);
        Assert.Contains("Merkez", dashboard.Agent.Tesisler.Select(x => x.Ad));
    }

    private static AgentEnrollmentCoordinator CreateCoordinator(
        IAgentPathResolver paths,
        IAgentBootstrapConfigurationStore bootstrapStore,
        IAgentCredentialStore credentialStore,
        RecordingAgentApiClient client,
        AgentTokenStore tokenStore,
        IAgentAuthenticationState authState,
        IAgentRuntimeStatus runtimeStatus,
        bool connectionSuccess = true)
    {
        return new AgentEnrollmentCoordinator(
            bootstrapStore,
            connectionSuccess ? new SuccessConnectionTester() : new FailingConnectionTester(),
            credentialStore,
            client,
            tokenStore,
            authState,
            runtimeStatus,
            paths,
            Options.Create(new StysAgentClientOptions
            {
                BaseUrl = "https://example.org/stys/api",
                RequestTimeoutSeconds = 30,
                AgentVersion = "1.0.0"
            }),
            NullLogger<AgentEnrollmentCoordinator>.Instance);
    }

    private sealed class TempAgentPathResolver : IAgentPathResolver
    {
        public TempAgentPathResolver(string root) => DataDirectory = root;
        public string DataDirectory { get; }
        public string BootstrapConfigurationPath => Path.Combine(DataDirectory, "bootstrap.json");
        public string CredentialStorePath => Path.Combine(DataDirectory, "credential.dat");
        public string LocalDevicesStorePath => Path.Combine(DataDirectory, "local-devices.json");
        public string PavoPairingStorePath => Path.Combine(DataDirectory, "pavo-pairing.dat");
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

    private sealed class FailingConnectionTester : IAgentBootstrapConnectionTester
    {
        public Task<AgentBootstrapConnectionTestResult> TestAsync(string baseUrl, int timeoutSeconds, CancellationToken cancellationToken) =>
            Task.FromResult(new AgentBootstrapConnectionTestResult
            {
                Success = false,
                Status = "timeout",
                Message = "STYS erişilemiyor."
            });
    }

    private sealed class RecordingAgentApiClient : IStysAgentApiClient
    {
        public int EnrollCallCount { get; private set; }
        public int TokenCallCount { get; private set; }
        public AgentEnrollmentResponse? EnrollResponse { get; set; }
        public AgentTokenResponse? TokenResponse { get; set; }
        public AgentSelfDto? SelfResponse { get; set; }

        public Task<AgentEnrollmentResponse> EnrollAsync(AgentEnrollmentRequest request, CancellationToken cancellationToken)
        {
            EnrollCallCount++;
            return Task.FromResult(EnrollResponse ?? new AgentEnrollmentResponse
            {
                AgentId = 1,
                ClientId = "client",
                ClientSecret = "secret",
                AgentKey = request.AgentKey
            });
        }

        public Task<AgentTokenResponse> GetTokenAsync(AgentTokenRequest request, CancellationToken cancellationToken)
        {
            TokenCallCount++;
            return Task.FromResult(TokenResponse ?? new AgentTokenResponse
            {
                AccessToken = "jwt",
                ExpiresAt = DateTime.UtcNow.AddMinutes(30)
            });
        }

        public Task SendHeartbeatAsync(AgentHeartbeatRequest request, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<AgentConfigDto?> GetConfigurationAsync(long currentVersion, CancellationToken cancellationToken) => Task.FromResult<AgentConfigDto?>(null);
        public Task<AgentSelfDto> GetMeAsync(CancellationToken cancellationToken) => Task.FromResult(SelfResponse ?? new AgentSelfDto());
        public Task<IReadOnlyCollection<AgentCommandDto>> GetPendingCommandsAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<AgentCommandDto>>([]);
        public Task AcceptCommandAsync(Guid commandId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SetRunningCommandAsync(Guid commandId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task CompleteCommandAsync(Guid commandId, AgentCommandCompleteRequest request, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task FailCommandAsync(Guid commandId, AgentCommandCompleteRequest request, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task RejectCommandAsync(Guid commandId, AgentCommandCompleteRequest request, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
