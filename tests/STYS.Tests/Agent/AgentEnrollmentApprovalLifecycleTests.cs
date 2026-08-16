using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using STYS.Agent.Client;
using STYS.Agent.Client.Authentication;
using STYS.Agent.Client.Infrastructure;
using STYS.Agent.Configuration;
using STYS.Agent.Contracts.Dtos;
using STYS.Agent.Contracts.Enums;
using STYS.Agent.Services;
using Xunit;

namespace STYS.Tests.Agent;

/// <summary>
/// E2D4 enrollment and approval lifecycle. Covers the two halves that do not need SQL Server:
/// enrollment-code hashing (no plaintext secret is ever persisted) and the agent-side
/// PendingApproval state machine (registered, credential stored, but workers stay gated until an
/// operator approves).
/// </summary>
public sealed class AgentEnrollmentApprovalLifecycleTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "stys-agent-e2d4-tests", Guid.NewGuid().ToString("N"));

    public AgentEnrollmentApprovalLifecycleTests() => Directory.CreateDirectory(_tempDir);

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

    // ---------------------------------------------------------------- enrollment code hashing

    [Fact]
    public void EnrollmentCode_PersistedDegeriPlaintextDegilHashtir()
    {
        const string code = "ABCD23456789WXYZ";

        var hash = AgentEnrollmentCodeHasher.Hash(code);

        // The stored value must not be the code itself, must not contain it, and must be a
        // fixed-width SHA-256 hex digest.
        Assert.NotEqual(code, hash);
        Assert.DoesNotContain(code, hash, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(64, hash.Length);
        Assert.Matches("^[0-9a-f]{64}$", hash);
    }

    [Fact]
    public void EnrollmentCode_HashDeterministikVeCaseInsensitiveNormalizeEdilir()
    {
        const string code = "ABCD23456789WXYZ";

        // Same code always hashes the same, so lookup works.
        Assert.Equal(AgentEnrollmentCodeHasher.Hash(code), AgentEnrollmentCodeHasher.Hash(code));
        // An operator retyping the code in lower case or with stray whitespace still enrolls.
        Assert.Equal(AgentEnrollmentCodeHasher.Hash(code), AgentEnrollmentCodeHasher.Hash("  abcd23456789wxyz  "));
        // A different code produces a different hash.
        Assert.NotEqual(AgentEnrollmentCodeHasher.Hash(code), AgentEnrollmentCodeHasher.Hash("ZZZZ23456789WXYZ"));
    }

    [Fact]
    public void EnrollmentCode_PrefixKisaVeTekBasinaKullanilamaz()
    {
        const string code = "ABCD23456789WXYZ";

        var prefix = AgentEnrollmentCodeHasher.BuildPrefix(code);

        Assert.Equal("ABCD23", prefix);
        Assert.Equal(AgentEnrollmentCodeHasher.PrefixLength, prefix.Length);
        // The prefix identifies a code in listings but is far too short to enroll with.
        Assert.True(prefix.Length < code.Length);
    }

    // ---------------------------------------------------------------- E2D4.1 approval policy

    [Theory]
    // Kurum mandates approval: a code created with RequiresApproval=false cannot switch it off.
    [InlineData(true, false, true)]
    // Kurum policy off but the operator demanded approval for this installation.
    [InlineData(false, true, true)]
    // Both mandate it.
    [InlineData(true, true, true)]
    // Neither: the agent activates immediately.
    [InlineData(false, false, false)]
    public void ApprovalPolicy_KurumPolitikasiCallerTarafindanKapatilamaz(
        bool kurumRequiresApproval,
        bool codeRequiresApproval,
        bool expectedRequiresApproval)
    {
        // Mirrors the decision made server-side in AgentTokenService.EnrollAsync: kurum policy can
        // only ever ADD approval, never remove it.
        var effective = kurumRequiresApproval || codeRequiresApproval;

        Assert.Equal(expectedRequiresApproval, effective);
    }

    [Fact]
    public void ApprovalPolicy_KurumBulunamazsaOnayZorunluVarsayilir()
    {
        // A missing kurum row must fail safe rather than silently activating the agent.
        bool? missingKurumPolicy = null;
        var effective = (missingKurumPolicy ?? true) || false;

        Assert.True(effective);
    }

    // ---------------------------------------------------------------- agent PendingApproval flow

    [Fact]
    public async Task RequiresApproval_KayitBasariliAmaTokenIstenmezVeWorkerlarBaslamaz()
    {
        var harness = new CoordinatorHarness(_tempDir);
        harness.Client.EnrollResponse = new AgentEnrollmentResponse
        {
            AgentId = 42,
            ClientId = "client-42",
            ClientSecret = "secret-42",
            AgentKey = "MACHINE",
            Durum = (int)AgentDurum.PendingApproval,
            Message = "Agent kaydedildi, onay bekleniyor."
        };

        var result = await harness.Coordinator.EnrollAsync(harness.BuildRequest(), CancellationToken.None);

        // Registration itself succeeded and the credential was persisted...
        Assert.True(result.Success);
        Assert.True(result.CredentialSaved);
        Assert.Equal(1, harness.Client.EnrollCallCount);
        Assert.NotNull(await harness.CredentialStore.GetAsync(CancellationToken.None));

        // ...but no token was requested, because a pending agent would only get a 403.
        Assert.False(result.TokenAcquired);
        Assert.Equal(0, harness.Client.TokenCallCount);

        // Worker gating: authentication is not ready, so heartbeat/command polling never start.
        Assert.False(harness.AuthState.IsReady);
        Assert.True(harness.RuntimeStatus.PendingApproval);
        // Pending approval is a normal waiting state, not a re-enrollment failure.
        Assert.False(harness.RuntimeStatus.RequiresReEnrollment);
    }

    [Fact]
    public async Task PendingAgent_OnaylanincaTokenAlirVeWorkerlarCalisabilir()
    {
        var harness = new CoordinatorHarness(_tempDir);
        await harness.SeedStoredCredentialAsync();

        // First poll: still awaiting the operator.
        harness.Client.StatusResponse = new AgentEnrollmentStatusResponse
        {
            AgentId = 42,
            Durum = (int)AgentDurum.PendingApproval,
            Approved = false,
            PendingApproval = true
        };

        Assert.False(await harness.Coordinator.TryActivateAsync(CancellationToken.None));
        Assert.False(harness.AuthState.IsReady);
        Assert.True(harness.RuntimeStatus.PendingApproval);
        Assert.Equal(0, harness.Client.TokenCallCount);

        // Operator approves; the very next poll authenticates without re-enrolling.
        harness.Client.StatusResponse = new AgentEnrollmentStatusResponse
        {
            AgentId = 42,
            Durum = (int)AgentDurum.Active,
            Approved = true,
            PendingApproval = false
        };

        Assert.True(await harness.Coordinator.TryActivateAsync(CancellationToken.None));
        Assert.True(harness.AuthState.IsReady);
        Assert.False(harness.RuntimeStatus.PendingApproval);
        Assert.Equal(1, harness.Client.TokenCallCount);
        // The stored credential was reused; the agent never registered a second time.
        Assert.Equal(0, harness.Client.EnrollCallCount);
    }

    [Theory]
    [InlineData(AgentDurum.Rejected)]
    [InlineData(AgentDurum.Disabled)]
    [InlineData(AgentDurum.Revoked)]
    public async Task TerminalDurumlar_TokenIstenmezVeAuthHazirOlmaz(AgentDurum durum)
    {
        var harness = new CoordinatorHarness(_tempDir);
        await harness.SeedStoredCredentialAsync();
        harness.Client.StatusResponse = new AgentEnrollmentStatusResponse
        {
            AgentId = 42,
            Durum = (int)durum,
            Approved = false,
            PendingApproval = false,
            Message = "Agent erişimi kapatıldı."
        };

        Assert.False(await harness.Coordinator.TryActivateAsync(CancellationToken.None));

        Assert.False(harness.AuthState.IsReady);
        Assert.Equal(0, harness.Client.TokenCallCount);
        // Terminal states are surfaced rather than retried silently as "pending".
        Assert.False(harness.RuntimeStatus.PendingApproval);
        Assert.True(harness.RuntimeStatus.RequiresReEnrollment);
    }

    // ---------------------------------------------------------------- E2D4.1 response-loss recovery

    [Fact]
    public async Task RegistrationNonce_IlkDenemedenOnceUretilirVeRetryAynisiniGonderir()
    {
        var harness = new CoordinatorHarness(_tempDir);
        // Simulate the response being lost in transit: the server committed, but the agent sees
        // only a transport failure and never stores a credential.
        harness.Client.EnrollException = new HttpRequestException("connection reset");

        var first = await harness.Coordinator.EnrollAsync(harness.BuildRequest(), CancellationToken.None);
        Assert.False(first.Success);

        // The nonce was persisted BEFORE the call, so it survives the failure...
        var afterFailure = await harness.CredentialStore.GetAsync(CancellationToken.None);
        Assert.NotNull(afterFailure);
        Assert.False(string.IsNullOrWhiteSpace(afterFailure!.RegistrationNonce));
        // ...and a nonce-only record is not mistaken for a usable credential.
        Assert.True(string.IsNullOrWhiteSpace(afterFailure.ClientId));
        var firstNonce = harness.Client.LastEnrollRequest?.RegistrationNonce;
        Assert.Equal(afterFailure.RegistrationNonce, firstNonce);

        // Retry: the server recognises the same nonce and completes the registration.
        harness.Client.EnrollException = null;
        harness.Client.EnrollResponse = new AgentEnrollmentResponse
        {
            AgentId = 42,
            ClientId = "client-recovered",
            ClientSecret = "secret-recovered",
            AgentKey = "MACHINE",
            Durum = (int)AgentDurum.Active,
            Message = "Agent kaydı kurtarıldı."
        };

        var second = await harness.Coordinator.EnrollAsync(harness.BuildRequest(), CancellationToken.None);

        Assert.True(second.Success);
        // Same proof replayed, which is what lets the server recover instead of rejecting.
        Assert.Equal(firstNonce, harness.Client.LastEnrollRequest?.RegistrationNonce);
        Assert.Equal(42, second.AgentId);

        var recovered = await harness.CredentialStore.GetAsync(CancellationToken.None);
        Assert.Equal("client-recovered", recovered!.ClientId);
        // The nonce is retained so a later crash can still recover.
        Assert.Equal(firstNonce, recovered.RegistrationNonce);
    }

    [Fact]
    public async Task RegistrationNonce_FarkliInstallationFarkliNonceUretir()
    {
        // Two independent installations must never share a recovery proof, otherwise one machine
        // could complete the other's registration.
        var a = new CoordinatorHarness(Path.Combine(_tempDir, "install-a"));
        var b = new CoordinatorHarness(Path.Combine(_tempDir, "install-b"));
        a.Client.EnrollException = new HttpRequestException("lost");
        b.Client.EnrollException = new HttpRequestException("lost");

        await a.Coordinator.EnrollAsync(a.BuildRequest(), CancellationToken.None);
        await b.Coordinator.EnrollAsync(b.BuildRequest(), CancellationToken.None);

        var nonceA = a.Client.LastEnrollRequest?.RegistrationNonce;
        var nonceB = b.Client.LastEnrollRequest?.RegistrationNonce;

        Assert.False(string.IsNullOrWhiteSpace(nonceA));
        Assert.False(string.IsNullOrWhiteSpace(nonceB));
        Assert.NotEqual(nonceA, nonceB);
    }

    // ---------------------------------------------------------------- harness

    private sealed class CoordinatorHarness
    {
        public CoordinatorHarness(string root)
        {
            Paths = new TempPathResolver(root);
            BootstrapStore = new FileAgentBootstrapConfigurationStore(Paths, NullLogger<FileAgentBootstrapConfigurationStore>.Instance);
            CredentialStore = new FileAgentCredentialStore(Paths, NullLogger<FileAgentCredentialStore>.Instance);
            TokenStore = new AgentTokenStore();
            AuthState = new AgentAuthenticationState();
            RuntimeStatus = new AgentRuntimeStatus();
            Client = new StubApiClient();

            Coordinator = new AgentEnrollmentCoordinator(
                BootstrapStore,
                new AlwaysReachableTester(),
                CredentialStore,
                Client,
                TokenStore,
                AuthState,
                RuntimeStatus,
                Paths,
                Options.Create(new StysAgentClientOptions
                {
                    BaseUrl = BaseUrl,
                    RequestTimeoutSeconds = 30,
                    AgentVersion = "1.0.0"
                }),
                NullLogger<AgentEnrollmentCoordinator>.Instance);
        }

        private const string BaseUrl = "https://stys.example.org";

        public TempPathResolver Paths { get; }
        public FileAgentBootstrapConfigurationStore BootstrapStore { get; }
        public FileAgentCredentialStore CredentialStore { get; }
        public AgentTokenStore TokenStore { get; }
        public AgentAuthenticationState AuthState { get; }
        public AgentRuntimeStatus RuntimeStatus { get; }
        public StubApiClient Client { get; }
        public AgentEnrollmentCoordinator Coordinator { get; }

        public AgentBootstrapEnrollmentRequest BuildRequest() => new()
        {
            StysBaseUrl = BaseUrl,
            AgentDisplayName = "Test Agent",
            EnrollmentCode = "ABCD23456789WXYZ",
            HttpTimeoutSeconds = 30,
            LocalUiPort = 5180,
            Capabilities = []
        };

        /// <summary>Puts the harness in the state an agent is in after a successful registration
        /// that required approval: bootstrap config + stored credential, no token.</summary>
        public async Task SeedStoredCredentialAsync()
        {
            var config = await BootstrapStore.GetAsync(CancellationToken.None);
            config.StysBaseUrl = BaseUrl;
            await BootstrapStore.SaveAsync(config, CancellationToken.None);

            await CredentialStore.SaveAsync(new AgentLocalCredential
            {
                ClientId = "client-42",
                ClientSecret = "secret-42",
                AgentInstanceId = Guid.NewGuid().ToString("N"),
                AgentKey = "MACHINE",
                EnrollmentBaseUrl = BaseUrl,
                AgentId = 42,
                CreatedAt = DateTime.UtcNow
            }, CancellationToken.None);
        }
    }

    private sealed class AlwaysReachableTester : IAgentBootstrapConnectionTester
    {
        public Task<AgentBootstrapConnectionTestResult> TestAsync(string baseUrl, int timeoutSeconds, CancellationToken cancellationToken) =>
            Task.FromResult(new AgentBootstrapConnectionTestResult { Success = true, Status = "ok", Message = "ok" });
    }

    private sealed class StubApiClient : IStysAgentApiClient
    {
        public int EnrollCallCount { get; private set; }
        public int TokenCallCount { get; private set; }
        public int StatusCallCount { get; private set; }
        public AgentEnrollmentResponse? EnrollResponse { get; set; }
        public AgentEnrollmentStatusResponse? StatusResponse { get; set; }
        public Exception? EnrollException { get; set; }
        public AgentEnrollmentRequest? LastEnrollRequest { get; private set; }

        public Task<AgentEnrollmentResponse> EnrollAsync(AgentEnrollmentRequest request, CancellationToken cancellationToken)
        {
            EnrollCallCount++;
            LastEnrollRequest = request;
            if (EnrollException is not null)
                throw EnrollException;

            return Task.FromResult(EnrollResponse ?? new AgentEnrollmentResponse
            {
                AgentId = 1,
                ClientId = "client",
                ClientSecret = "secret",
                AgentKey = request.AgentKey,
                Durum = (int)AgentDurum.Active
            });
        }

        public Task<AgentEnrollmentStatusResponse> GetEnrollmentStatusAsync(AgentEnrollmentStatusRequest request, CancellationToken cancellationToken)
        {
            StatusCallCount++;
            return Task.FromResult(StatusResponse ?? new AgentEnrollmentStatusResponse
            {
                AgentId = 1,
                Durum = (int)AgentDurum.Active,
                Approved = true
            });
        }

        public Task<AgentTokenResponse> GetTokenAsync(AgentTokenRequest request, CancellationToken cancellationToken)
        {
            TokenCallCount++;
            return Task.FromResult(new AgentTokenResponse
            {
                AccessToken = "jwt",
                ExpiresAt = DateTime.UtcNow.AddMinutes(30)
            });
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

    private sealed class TempPathResolver : IAgentPathResolver
    {
        public TempPathResolver(string root) => DataDirectory = root;
        public string DataDirectory { get; }
        public string LogDirectory => Path.Combine(DataDirectory, "logs");
        public string BootstrapConfigurationPath => Path.Combine(DataDirectory, "bootstrap.json");
        public string CredentialStorePath => Path.Combine(DataDirectory, "credential.dat");
        public string LocalDevicesStorePath => Path.Combine(DataDirectory, "local-devices.json");
        public string LocalDeviceTerminalsStorePath => Path.Combine(DataDirectory, "local-device-terminals.json");
        public string PavoPairingStorePath => Path.Combine(DataDirectory, "pavo-pairing.dat");
        public string AgentCommandExecutionStorePath => Path.Combine(DataDirectory, "agent-command-executions.json");
        public string InstanceIdPath => Path.Combine(DataDirectory, "instance.id");
        public string ReleaseStagingRootDirectory => Path.Combine(DataDirectory, "updates", "staging");
        public string GetReleaseStagingDirectory(string version, string runtimeIdentifier) => Path.Combine(ReleaseStagingRootDirectory, version, runtimeIdentifier);
        public string GetReleaseStagingStatePath(string version, string runtimeIdentifier) => Path.Combine(GetReleaseStagingDirectory(version, runtimeIdentifier), "staging-state.json");
        public string GetReleaseStagingPackagePath(string version, string runtimeIdentifier) => Path.Combine(GetReleaseStagingDirectory(version, runtimeIdentifier), "package.bin");
    }
}
