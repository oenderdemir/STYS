using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using STYS.Agent.Client.Authentication;
using STYS.Agent.Client.Infrastructure;
using STYS.Agent.Configuration;
using STYS.Agent.LocalManagement;
using STYS.Agent.Services;

namespace STYS.Tests.Agent;

public sealed class AgentLocalManagementPhaseATests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "stys-agent-a1-tests", Guid.NewGuid().ToString("N"));

    public AgentLocalManagementPhaseATests()
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
    public async Task BootstrapConfig_SaveLoad_PreservesValues()
    {
        var store = CreateStore();
        var input = new AgentBootstrapConfiguration
        {
            StysBaseUrl = "https://trt.kamutesis.com/stys/api/",
            LocalUiPort = 5180,
            AgentDisplayName = "Resepsiyon Agent",
            HttpTimeoutSeconds = 45
        };

        await store.SaveAsync(input, CancellationToken.None);
        var loaded = await store.GetAsync(CancellationToken.None);

        Assert.Equal("https://trt.kamutesis.com/stys/api", loaded.StysBaseUrl);
        Assert.Equal(5180, loaded.LocalUiPort);
        Assert.Equal("Resepsiyon Agent", loaded.AgentDisplayName);
        Assert.Equal(45, loaded.HttpTimeoutSeconds);
    }

    [Fact]
    public async Task BootstrapConfig_RestartSonrasi_Korunur()
    {
        var store1 = CreateStore();
        await store1.SaveAsync(new AgentBootstrapConfiguration
        {
            StysBaseUrl = "https://example.org/stys/api",
            LocalUiPort = 5199,
            AgentDisplayName = "Test Agent",
            HttpTimeoutSeconds = 19
        }, CancellationToken.None);

        var store2 = CreateStore();
        var loaded = await store2.GetAsync(CancellationToken.None);

        Assert.Equal("https://example.org/stys/api", loaded.StysBaseUrl);
        Assert.Equal(5199, loaded.LocalUiPort);
        Assert.Equal("Test Agent", loaded.AgentDisplayName);
        Assert.Equal(19, loaded.HttpTimeoutSeconds);
    }

    [Fact]
    public async Task BootstrapConfig_InvalidUrl_Reddedilir()
    {
        var store = CreateStore();
        var tester = CreateManagementService(store, CreateConnectionTester(new SuccessfulConnectionTesterHandler()), credentialPresent: false);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            tester.SaveConfigurationAsync(new AgentBootstrapConfiguration
            {
                StysBaseUrl = "ftp://invalid",
                LocalUiPort = 5180,
                AgentDisplayName = "A",
                HttpTimeoutSeconds = 30
            }, CancellationToken.None));
    }

    [Fact]
    public async Task BootstrapJson_CredentialSecretVeyaEnrollmentCode_Icermez()
    {
        var resolver = CreatePathResolver();
        var store = new FileAgentBootstrapConfigurationStore(resolver, NullLogger<FileAgentBootstrapConfigurationStore>.Instance);
        await store.SaveAsync(new AgentBootstrapConfiguration
        {
            StysBaseUrl = "https://example.org/stys/api",
            LocalUiPort = 5180,
            AgentDisplayName = "A",
            HttpTimeoutSeconds = 30
        }, CancellationToken.None);

        var json = await File.ReadAllTextAsync(resolver.BootstrapConfigurationPath);
        Assert.DoesNotContain("ClientSecret", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EnrollmentCode", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LocalUi_SadeceLoopback_BinderUretir()
    {
        var endpoint = AgentLocalWebHostBinding.CreateLoopbackEndpoint(5180);

        Assert.Equal(IPAddress.Loopback, endpoint.Address);
        Assert.Equal(5180, endpoint.Port);
    }

    [Fact]
    public async Task ConnectionTest_Success_EndpointPathiniDogruKurur()
    {
        var handler = new RecordingHttpMessageHandler(() => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { status = "ok", serverTime = "2026-08-11T10:00:00Z", version = "1.2.3" })
        });
        var tester = CreateConnectionTester(handler);

        var result = await tester.TestAsync("https://trt.kamutesis.com/stys/api/", 30, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("ok", result.Status);
        Assert.Equal("Bağlantı başarılı.", result.Message);
        Assert.Equal("2026-08-11T10:00:00Z", result.ServerTime);
        Assert.Equal("1.2.3", result.Version);
        Assert.Equal("https://trt.kamutesis.com/stys/api/agent/bootstrap/ping", handler.LastRequestUri!.ToString());
    }

    [Theory]
    [InlineData("http://example.org", "timeout")]
    [InlineData("http://example.org", "dns")]
    [InlineData("http://example.org", "refused")]
    [InlineData("http://example.org", "tls")]
    public async Task ConnectionTest_HataEslestirme_Calisir(string baseUrl, string kind)
    {
        var handler = kind switch
        {
            "timeout" => new ThrowingHttpMessageHandler(() => throw new TaskCanceledException("timeout")),
            "dns" => new ThrowingHttpMessageHandler(() => throw new HttpRequestException("dns", new SocketException((int)SocketError.HostNotFound))),
            "refused" => new ThrowingHttpMessageHandler(() => throw new HttpRequestException("refused", new SocketException((int)SocketError.ConnectionRefused))),
            "tls" => new ThrowingHttpMessageHandler(() => throw new HttpRequestException("tls", new AuthenticationException("tls"))),
            _ => throw new InvalidOperationException()
        };

        var tester = CreateConnectionTester(handler);
        var result = await tester.TestAsync(baseUrl, 1, CancellationToken.None);

        Assert.False(result.Success);
        Assert.NotEqual("unknown", result.Status);
    }

    [Fact]
    public async Task Dashboard_KrediKartBilgisiDetayIcermez_BoolGosterir()
    {
        var store = CreateStore();
        await store.SaveAsync(new AgentBootstrapConfiguration
        {
            StysBaseUrl = "https://example.org/stys/api",
            LocalUiPort = 5180,
            AgentDisplayName = "A",
            HttpTimeoutSeconds = 30
        }, CancellationToken.None);

        var service = new AgentBootstrapManagementService(
            store,
            CreateConnectionTester(new SuccessfulConnectionTesterHandler()),
            new FakeCredentialStore(new AgentLocalCredential { ClientId = "client", ClientSecret = "secret", AgentInstanceId = "instance", AgentId = 1, CreatedAt = DateTime.UtcNow }),
            new FakeAuthenticationState(false),
            new AgentBootstrapConnectionTestState());

        var dashboard = await service.GetDashboardAsync(CancellationToken.None);

        Assert.True(dashboard.CredentialMevcutMu);
        Assert.Equal("Kayıtlı", dashboard.EnrollmentDurumu);
        Assert.Equal("A", dashboard.AgentDisplayName);
        Assert.Equal("Başlatıldı", dashboard.AgentDurumu);
        Assert.False(string.IsNullOrWhiteSpace(dashboard.LocalUiVersion));
    }

    private FileAgentBootstrapConfigurationStore CreateStore() =>
        new(CreatePathResolver(), NullLogger<FileAgentBootstrapConfigurationStore>.Instance);

    private TempAgentPathResolver CreatePathResolver() => new(_tempDir);

    private AgentBootstrapManagementService CreateManagementService(
        IAgentBootstrapConfigurationStore store,
        IAgentBootstrapConnectionTester tester,
        bool credentialPresent)
    {
        return new AgentBootstrapManagementService(
            store,
            tester,
            new FakeCredentialStore(credentialPresent ? new AgentLocalCredential { ClientId = "c", ClientSecret = "s", AgentInstanceId = "i", AgentId = 1, CreatedAt = DateTime.UtcNow } : null),
            new FakeAuthenticationState(false),
            new AgentBootstrapConnectionTestState());
    }

    private static IAgentBootstrapConnectionTester CreateConnectionTester(HttpMessageHandler handler)
    {
        var factory = new FakeHttpClientFactory(handler);
        return new AgentBootstrapConnectionTester(factory);
    }

    private sealed class TempAgentPathResolver : IAgentPathResolver
    {
        public TempAgentPathResolver(string root) => DataDirectory = root;
        public string DataDirectory { get; }
        public string BootstrapConfigurationPath => Path.Combine(DataDirectory, "bootstrap.json");
        public string CredentialStorePath => Path.Combine(DataDirectory, "credential.dat");
        public string InstanceIdPath => Path.Combine(DataDirectory, "instance.id");
    }

    private sealed class FakeCredentialStore : IAgentCredentialStore
    {
        private AgentLocalCredential? _credential;
        public FakeCredentialStore(AgentLocalCredential? credential) => _credential = credential;
        public Task<AgentLocalCredential?> GetAsync(CancellationToken cancellationToken) => Task.FromResult(_credential);
        public Task SaveAsync(AgentLocalCredential credential, CancellationToken cancellationToken)
        {
            _credential = credential;
            return Task.CompletedTask;
        }
        public Task DeleteAsync(CancellationToken cancellationToken)
        {
            _credential = null;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAuthenticationState : IAgentAuthenticationState
    {
        public FakeAuthenticationState(bool ready) => IsReady = ready;
        public bool IsReady { get; }
        public Task WaitUntilReadyAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public void MarkAuthenticated() { }
    }

    private sealed class FakeHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;
        public FakeHttpClientFactory(HttpMessageHandler handler) => _handler = handler;
        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
    }

    private sealed class SuccessfulConnectionTesterHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { status = "ok", serverTime = "2026-08-11T10:00:00Z", version = "1.2.3" })
            });
        }
    }

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpResponseMessage> _factory;
        public Uri? LastRequestUri { get; private set; }
        public RecordingHttpMessageHandler(Func<HttpResponseMessage> factory) => _factory = factory;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            return Task.FromResult(_factory());
        }
    }

    private sealed class ThrowingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Action _throw;
        public ThrowingHttpMessageHandler(Action throwAction) => _throw = throwAction;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _throw();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
