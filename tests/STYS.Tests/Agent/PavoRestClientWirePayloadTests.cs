using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using STYS.Agent.Contracts.Dtos;
using STYS.Agent.Modules.Pavo;

namespace STYS.Tests.Agent;

public sealed class PavoRestClientWirePayloadTests
{
    [Fact]
    public async Task Pairing_Body_OnlyTransactionHandle_ContainsSampleFields()
    {
        var handler = new CapturingHandler();
        var client = CreateClient(handler);

        await client.PairingAsync(new PavoPairingRequest
        {
            PosCihaziId = 42,
            IpAddress = "10.0.0.5",
            HttpPort = 4567,
            UseHttps = false,
            CurrentFingerprint = "FP-LOCAL",
            TransactionHandle = CreateHandle()
        }, CancellationToken.None);

        var json = handler.LastBody ?? throw new Xunit.Sdk.XunitException("Request body missing.");
        using var doc = JsonDocument.Parse(json);

        Assert.True(doc.RootElement.TryGetProperty("TransactionHandle", out var handle));
        Assert.False(doc.RootElement.TryGetProperty("PosCihaziId", out _));
        Assert.False(doc.RootElement.TryGetProperty("IpAddress", out _));
        Assert.False(doc.RootElement.TryGetProperty("CurrentFingerprint", out _));
        Assert.Equal("SN-001", handle.GetProperty("SerialNumber").GetString());
        Assert.Equal("FP-001", handle.GetProperty("Fingerprint").GetString());
        Assert.Equal(7, handle.GetProperty("TransactionSequence").GetInt64());
        Assert.Equal("2026-08-13T17:00:01.123456", handle.GetProperty("TransactionDate").GetString());
    }

    [Fact]
    public async Task DeviceCommand_Body_OnlyTransactionHandle_ContainsSampleFields()
    {
        var handler = new CapturingHandler();
        var client = CreateClient(handler);

        await client.PingAsync(new PavoPingRequest
        {
            PosCihaziId = 42,
            IpAddress = "10.0.0.5",
            HttpPort = 4567,
            TransactionHandle = CreateHandle()
        }, CancellationToken.None);

        var json = handler.LastBody ?? throw new Xunit.Sdk.XunitException("Request body missing.");
        using var doc = JsonDocument.Parse(json);

        Assert.True(doc.RootElement.TryGetProperty("TransactionHandle", out _));
        Assert.False(doc.RootElement.TryGetProperty("PosCihaziId", out _));
        Assert.False(doc.RootElement.TryGetProperty("IpAddress", out _));
    }

    [Fact]
    public async Task StartPayment_Body_MatchesNestedSampleShape()
    {
        var handler = new CapturingHandler();
        var client = CreateClient(handler);

        await client.StartPaymentAsync(new PavoStartPaymentRequest
        {
            PosCihaziId = 99,
            PosOdemeIslemiId = 77,
            PosTerminalId = 11,
            SaleReference = "SALE-123",
            Amount = 125.50m,
            CurrencyCode = "TRY",
            Description = "Test ödeme",
            IpAddress = "10.0.0.5",
            HttpPort = 4567,
            TransactionHandle = CreateHandle()
        }, CancellationToken.None);

        var json = handler.LastBody ?? throw new Xunit.Sdk.XunitException("Request body missing.");
        using var doc = JsonDocument.Parse(json);

        Assert.True(doc.RootElement.TryGetProperty("TransactionHandle", out _));
        Assert.True(doc.RootElement.TryGetProperty("Payment", out var payment));
        Assert.False(doc.RootElement.TryGetProperty("PosCihaziId", out _));
        Assert.False(doc.RootElement.TryGetProperty("IpAddress", out _));
        Assert.Equal(125.50m, payment.GetProperty("Amount").GetDecimal());
        Assert.Equal(0, payment.GetProperty("InstallmentCount").GetInt32());
        Assert.Equal("TRY", payment.GetProperty("CurrencyCode").GetString());
        Assert.Equal("SALE-123", payment.GetProperty("SaleReference").GetString());
        Assert.True(payment.GetProperty("SelectedSlots").EnumerateArray().Any());

        var additionalInfo = payment.GetProperty("AdditionalInfo");
        Assert.True(additionalInfo.TryGetProperty("print", out _));
        Assert.Equal("SALE-123", additionalInfo.GetProperty("qrCodeText").GetString());
        Assert.True(additionalInfo.GetProperty("list").EnumerateArray().Any());
    }

    [Fact]
    public async Task GetPaymentResult_Body_OnlyTransactionHandle()
    {
        var handler = new CapturingHandler();
        var client = CreateClient(handler);

        await client.GetPaymentResultAsync(new PavoGetPaymentResultRequest
        {
            PosCihaziId = 12,
            PosOdemeIslemiId = 34,
            PosTerminalId = 56,
            SaleReference = "SALE-999",
            IpAddress = "10.0.0.5",
            HttpPort = 4567,
            TransactionHandle = CreateHandle()
        }, CancellationToken.None);

        var json = handler.LastBody ?? throw new Xunit.Sdk.XunitException("Request body missing.");
        using var doc = JsonDocument.Parse(json);

        Assert.True(doc.RootElement.TryGetProperty("TransactionHandle", out _));
        Assert.False(doc.RootElement.TryGetProperty("PosCihaziId", out _));
        Assert.False(doc.RootElement.TryGetProperty("SaleReference", out _));
    }

    [Fact]
    public async Task BuildBaseUri_UseHttpsFalse_HttpsPortDoluOlsaBile_HttpKullanir()
    {
        var handler = new CapturingHandler();
        var client = CreateClient(handler);

        await client.PairingAsync(new PavoPairingRequest
        {
            PosCihaziId = 1,
            IpAddress = "10.0.0.9",
            HttpPort = 4567,
            HttpsPort = 4568,
            UseHttps = false,
            TransactionHandle = CreateHandle()
        }, CancellationToken.None);

        Assert.NotNull(handler.LastRequestUri);
        Assert.Equal("http", handler.LastRequestUri!.Scheme);
        Assert.Equal(4567, handler.LastRequestUri.Port);
        Assert.Equal("/Pairing", handler.LastRequestUri.AbsolutePath);
    }

    [Fact]
    public async Task Pairing_ReferenceResponse_HasErrorFalse_ErrorCodeSifir_BasariylaDeserializeOlurVeBasarilidir()
    {
        var handler = new CapturingHandler
        {
            // Sample shape from the working Pavo509.Client reference response.
            ResponseBody = "{\"HasError\":false,\"HasAbondon\":false,\"ErrorCode\":0,\"Message\":\"ok\"}"
        };
        var client = CreateClient(handler);

        var response = await client.PairingAsync(new PavoPairingRequest
        {
            PosCihaziId = 1,
            IpAddress = "10.0.0.9",
            HttpPort = 4567,
            TransactionHandle = CreateHandle()
        }, CancellationToken.None);

        Assert.False(response.HasError);
        Assert.False(response.HasAbondon);
        Assert.Equal(0, response.ErrorCode);
        Assert.True(PavoResponseHelpers.IsSuccessful(response));
    }

    [Fact]
    public async Task Pairing_ErrorCodeNull_Basarilidir()
    {
        var handler = new CapturingHandler
        {
            ResponseBody = "{\"HasError\":false,\"HasAbondon\":false,\"ErrorCode\":null}"
        };
        var client = CreateClient(handler);

        var response = await client.PairingAsync(new PavoPairingRequest
        {
            PosCihaziId = 1,
            IpAddress = "10.0.0.9",
            HttpPort = 4567,
            TransactionHandle = CreateHandle()
        }, CancellationToken.None);

        Assert.True(PavoResponseHelpers.IsSuccessful(response));
    }

    [Fact]
    public async Task Pairing_ErrorCodeSifirDegil_Basarisizdir()
    {
        var handler = new CapturingHandler
        {
            ResponseBody = "{\"HasError\":false,\"HasAbondon\":false,\"ErrorCode\":12}"
        };
        var client = CreateClient(handler);

        var response = await client.PairingAsync(new PavoPairingRequest
        {
            PosCihaziId = 1,
            IpAddress = "10.0.0.9",
            HttpPort = 4567,
            TransactionHandle = CreateHandle()
        }, CancellationToken.None);

        Assert.False(PavoResponseHelpers.IsSuccessful(response));
    }

    [Fact]
    public async Task Pairing_ReferansGovdesindeOlmayanAlanlarBasariyiEtkilemez()
    {
        // The reference PavoResponse has no OnayliMi/PairingId/PairingCode/Fingerprint fields at
        // all. A minimal reference-shaped body must still deserialize and read as success.
        var handler = new CapturingHandler
        {
            ResponseBody = "{\"HasError\":false,\"HasAbondon\":false,\"ErrorCode\":0}"
        };
        var client = CreateClient(handler);

        var response = await client.PairingAsync(new PavoPairingRequest
        {
            PosCihaziId = 1,
            IpAddress = "10.0.0.9",
            HttpPort = 4567,
            TransactionHandle = CreateHandle()
        }, CancellationToken.None);

        Assert.True(PavoResponseHelpers.IsSuccessful(response));
    }

    private static PavoRestClient CreateClient(CapturingHandler handler)
    {
        var factory = new SingleClientFactory(new HttpClient(handler)
        {
            BaseAddress = new Uri("http://127.0.0.1")
        });

        return new PavoRestClient(factory, NullLogger<PavoRestClient>.Instance);
    }

    private static PavoTransactionHandle CreateHandle() => new()
    {
        SerialNumber = "SN-001",
        Fingerprint = "FP-001",
        TransactionSequence = 7,
        TransactionDate = new DateTime(2026, 8, 13, 17, 0, 1, 123, DateTimeKind.Local).AddTicks(4560)
    };

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string? LastBody { get; private set; }
        public Uri? LastRequestUri { get; private set; }

        /// <summary>Minimal valid reference-shaped success envelope. An empty body is a hard failure
        /// under reference semantics, so request-shape tests need a real body here.</summary>
        public string ResponseBody { get; set; } = "{\"HasError\":false,\"HasAbondon\":false,\"ErrorCode\":0}";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ResponseBody, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class SingleClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public SingleClientFactory(HttpClient client) => _client = client;

        public HttpClient CreateClient(string name) => _client;
    }
}
