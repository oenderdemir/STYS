using Microsoft.Extensions.Logging.Abstractions;
using STYS.Agent.Contracts.Dtos;
using STYS.Agent.Modules.Pavo;
using Xunit;

namespace STYS.Tests.Agent;

/// <summary>
/// A connect timeout and a response timeout both surface as TaskCanceledException wrapping a
/// TimeoutException, and the client tells them apart by the nesting the runtime produces. That is
/// an implementation detail of the runtime, so it is asserted here against REAL timeouts: if a
/// future .NET changes the shape, these fail loudly instead of the classification silently
/// regressing and mislabelling payments.
/// </summary>
public sealed class PavoTimeoutClassificationTests
{
    // RFC 5737 TEST-NET-1 style unroutable address: the handshake never completes.
    private const string UnroutableHost = "10.255.255.1";

    private sealed class SingleClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class NeverRespondingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        }
    }

    private static PavoPingRequest NewPing(string host) => new()
    {
        IpAddress = host,
        HttpPort = 9,
        UseHttps = false,
        TransactionHandle = new PavoTransactionHandle
        {
            SerialNumber = "SN-1",
            Fingerprint = "FP",
            TransactionSequence = 1,
            TransactionDate = DateTime.UtcNow
        }
    };

    [Fact]
    public async Task GercekConnectTimeout_ConnectTimeoutOlarakSiniflanir()
    {
        var handler = new SocketsHttpHandler { ConnectTimeout = TimeSpan.FromSeconds(2) };
        using var httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(60) };
        var client = new PavoRestClient(new SingleClientFactory(httpClient), NullLogger<PavoRestClient>.Instance);

        var ex = await Assert.ThrowsAsync<PavoRestClientException>(
            () => client.PingAsync(NewPing(UnroutableHost), CancellationToken.None));

        Assert.Equal(PavoDeviceReachability.ConnectTimeout, ex.ErrorCode);
        Assert.True(PavoDeviceReachability.IsDeviceNeverReached(ex.ErrorCode));
        Assert.False(ex.HttpResponseReceived);
    }

    [Fact]
    public async Task GercekCevapTimeoutu_UlasilamadiSayilmaz()
    {
        // The connection succeeds (stub handler) and HttpClient.Timeout elapses instead. The device
        // may have acted on the request, so this must stay ambiguous.
        using var httpClient = new HttpClient(new NeverRespondingHandler()) { Timeout = TimeSpan.FromSeconds(2) };
        var client = new PavoRestClient(new SingleClientFactory(httpClient), NullLogger<PavoRestClient>.Instance);

        var ex = await Assert.ThrowsAsync<PavoRestClientException>(
            () => client.PingAsync(NewPing("127.0.0.1"), CancellationToken.None));

        Assert.Equal(PavoDeviceReachability.ResponseTimeout, ex.ErrorCode);
        Assert.False(PavoDeviceReachability.IsDeviceNeverReached(ex.ErrorCode));
        Assert.False(ex.HttpResponseReceived);
    }

    [Fact]
    public async Task ConnectTimeout_RequestTimeoutundanCokOnceDoner()
    {
        // The point of bounding the connect phase separately: an unplugged device is reported in
        // seconds rather than after the full request budget.
        var handler = new SocketsHttpHandler { ConnectTimeout = TimeSpan.FromSeconds(2) };
        using var httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(60) };
        var client = new PavoRestClient(new SingleClientFactory(httpClient), NullLogger<PavoRestClient>.Instance);

        var started = System.Diagnostics.Stopwatch.StartNew();
        await Assert.ThrowsAsync<PavoRestClientException>(
            () => client.PingAsync(NewPing(UnroutableHost), CancellationToken.None));
        started.Stop();

        Assert.True(started.Elapsed < TimeSpan.FromSeconds(20),
            $"connect timeout {started.Elapsed.TotalSeconds:0.0}s surdu; request timeout'una yaklasmamali.");
    }
}
