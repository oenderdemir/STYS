using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using STYS.Entegrasyonlar.Pos.Entities;
using STYS.Entegrasyonlar.Pos.Services;
using STYS.Entegrasyonlar.Pavo.Options;
using STYS.Entegrasyonlar.Pavo.Services;

namespace STYS.Tests;

public class PavoUniCloudClientTests
{
    [Fact]
    public void Pavo_SaglayiciBagimsizPosSozlesmesiniUygular()
    {
        IPosOdemeSaglayicisi saglayici = new PavoPosOdemeSaglayicisi(null!);

        Assert.Equal("PAVO", saglayici.Kod);
        Assert.True(saglayici.EslesmeDestekliyorMu);
        saglayici.TerminalBilgileriniDogrula(new PosTerminal
        {
            SaglayiciKodu = "PAVO",
            Ad = "Resepsiyon",
            SerialNumber = "PAV960000079",
            SourceFingerprint = "stys-3"
        });
    }

    [Fact]
    public async Task CreateLink_UniCloudCiftAsamaliTokenVeTerminalHedefiyleGonderilir()
    {
        var handler = new QueueHttpMessageHandler(
            """{"Result":"OK","AccessToken":"initial-token"}""",
            """{"Data":{"MerchantUid":"merchant-1"},"Success":true}""",
            """{"Data":"terminal-api-key","Success":true}""",
            """{"Result":"OK","AccessToken":"terminal-access-token"}""",
            """{"Data":{"Id":26438,"StatusId":1},"Success":true}""");
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://pavo.test") };
        var options = Options.Create(new PavoOptions
        {
            Enabled = true,
            BaseUrl = "https://pavo.test",
            AppToken = "app-token",
            ApiKey = "api-key"
        });
        var client = new PavoUniCloudClient(httpClient, options, NullLogger<PavoUniCloudClient>.Instance);
        var terminal = new PosTerminal
        {
            Id = 7,
            TesisId = 3,
            Ad = "Resepsiyon",
            SerialNumber = "PAV960000079",
            SourceFingerprint = "stys-3",
            SourceTerminalReference = "RESEPSIYON",
            TargetFingerprint = "target-fingerprint"
        };

        var result = await client.CreateLinkAsync(terminal, "STYS-REF-1", 125.50m, "TRY", CancellationToken.None);

        Assert.Equal(26438, result.Id);
        Assert.Equal(1, result.StatusId);
        Assert.Equal(5, handler.Requests.Count);
        Assert.Equal("/api/PaymentLinkIntegration/CreateLinkRequest", handler.Requests[4].Path);
        Assert.Equal("Bearer terminal-access-token", handler.Requests[4].Authorization);
        Assert.Contains("\"paymentLinkReference\":\"STYS-REF-1\"", handler.Requests[4].Body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"targetSerialNo\":\"PAV960000079\"", handler.Requests[4].Body, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class QueueHttpMessageHandler(params string[] responses) : HttpMessageHandler
    {
        private readonly Queue<string> _responses = new(responses);
        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new CapturedRequest(
                request.RequestUri?.AbsolutePath ?? string.Empty,
                request.Headers.Authorization?.ToString(),
                body));

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responses.Dequeue(), Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed record CapturedRequest(string Path, string? Authorization, string Body);
}
