using System.Net.Sockets;
using STYS.Agent.Contracts.Dtos;
using STYS.Agent.Modules.Pavo;
using Xunit;

namespace STYS.Tests.Agent;

/// <summary>
/// A payment may only be called failed when the device provably never saw the request. These pin
/// that a TCP connect failure is reported distinctly from a request that was sent and went
/// unanswered — the latter must stay ambiguous, because the card may already have been charged.
/// </summary>
public sealed class PavoDeviceReachabilityTests
{
    private sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(exception);
    }

    private static async Task<string> CaptureErrorCodeAsync(Exception transportException)
    {
        using var client = new HttpClient(new ThrowingHandler(transportException)) { Timeout = TimeSpan.FromSeconds(5) };
        var factory = new SingleClientFactory(client);
        var restClient = new PavoRestClient(factory, Microsoft.Extensions.Logging.Abstractions.NullLogger<PavoRestClient>.Instance);

        var ex = await Assert.ThrowsAsync<PavoRestClientException>(() =>
            restClient.PingAsync(new PavoPingRequest
            {
                IpAddress = "127.0.0.1",
                HttpPort = 4567,
                UseHttps = false,
                TransactionHandle = new PavoTransactionHandle
                {
                    SerialNumber = "SN-1",
                    Fingerprint = "FP",
                    TransactionSequence = 1,
                    TransactionDate = DateTime.UtcNow
                }
            }, CancellationToken.None));

        Assert.False(ex.HttpResponseReceived);
        return ex.ErrorCode;
    }

    private sealed class SingleClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    [Fact]
    public async Task ConnectTimeout_CevapTimeoutundanAyriKodDoner()
    {
        // SocketError.TimedOut arrives from the connect phase: no bytes reached the device.
        var code = await CaptureErrorCodeAsync(
            new HttpRequestException("connect timed out", new SocketException((int)SocketError.TimedOut)));

        Assert.Equal(PavoDeviceReachability.ConnectTimeout, code);
        Assert.True(PavoDeviceReachability.IsDeviceNeverReached(code));
    }

    [Fact]
    public async Task CevapTimeoutu_UlasilamadiSayilmaz()
    {
        // The request was sent and the device may have completed the payment.
        var code = await CaptureErrorCodeAsync(new TaskCanceledException("response timeout"));

        Assert.Equal(PavoDeviceReachability.ResponseTimeout, code);
        Assert.False(PavoDeviceReachability.IsDeviceNeverReached(code));
    }

    [Theory]
    [InlineData(SocketError.ConnectionRefused)]
    [InlineData(SocketError.HostUnreachable)]
    [InlineData(SocketError.NetworkUnreachable)]
    public async Task BaglantiKurulamayanDurumlar_UlasilamadiSayilir(SocketError socketError)
    {
        var code = await CaptureErrorCodeAsync(new HttpRequestException("connect failed", new SocketException((int)socketError)));

        Assert.True(PavoDeviceReachability.IsDeviceNeverReached(code));
    }

    [Theory]
    [InlineData("TLS_CERTIFICATE")]
    [InlineData("NETWORK")]
    [InlineData("BODY_READ_FAILED")]
    [InlineData("INVALID_REQUEST")]
    [InlineData(null)]
    [InlineData("")]
    public void BelirsizKodlar_UlasilamadiSayilmaz(string? code)
    {
        // Deliberately conservative: these can also surface after bytes were sent, so a payment
        // carrying them must stay Unknown rather than be declared failed.
        Assert.False(PavoDeviceReachability.IsDeviceNeverReached(code));
    }
}
