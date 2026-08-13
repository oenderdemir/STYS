using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using STYS.Agent.Contracts.Dtos;

namespace STYS.Agent.Modules.Pavo;

public sealed class PavoRestClient : IPavoRestClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PavoRestClient> _logger;

    public PavoRestClient(IHttpClientFactory httpClientFactory, ILogger<PavoRestClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public Task<PavoPairingResponse> PairingAsync(PavoPairingRequest request, CancellationToken cancellationToken) =>
        SendAsync<PavoPairingResponse>("Pairing", request, BuildPairingWireRequest(request), cancellationToken);

    public Task<PavoPingResponse> PingAsync(PavoPingRequest request, CancellationToken cancellationToken) =>
        SendAsync<PavoPingResponse>("Ping", request, BuildPingWireRequest(request), cancellationToken);

    public Task<PavoGetDeviceInfoResponse> GetDeviceInfoAsync(PavoGetDeviceInfoRequest request, CancellationToken cancellationToken) =>
        SendAsync<PavoGetDeviceInfoResponse>("GetDeviceInfo", request, BuildGetDeviceInfoWireRequest(request), cancellationToken);

    public Task<PavoStartPaymentResponse> StartPaymentAsync(PavoStartPaymentRequest request, CancellationToken cancellationToken) =>
        SendAsync<PavoStartPaymentResponse>("StartPayment", request, BuildStartPaymentWireRequest(request), cancellationToken);

    public Task<PavoGetPaymentResultResponse> GetPaymentResultAsync(PavoGetPaymentResultRequest request, CancellationToken cancellationToken) =>
        SendAsync<PavoGetPaymentResultResponse>("GetPaymentResult", request, BuildGetPaymentResultWireRequest(request), cancellationToken);

    private async Task<TResponse> SendAsync<TResponse>(string method, PavoDeviceRequestBase request, object wireRequest, CancellationToken cancellationToken)
        where TResponse : PavoBaseResponse, new()
    {
        ValidateRequest(request);

        var client = _httpClientFactory.CreateClient("PavoClient");
        client.Timeout = client.Timeout == Timeout.InfiniteTimeSpan ? TimeSpan.FromSeconds(30) : client.Timeout;

        var baseUri = BuildBaseUri(request);
        var uri = new Uri(baseUri, method);

        try
        {
            using var response = await client.PostAsync(uri, JsonContent.Create(wireRequest, wireRequest.GetType(), options: JsonOptions), cancellationToken);
            var result = await ReadResponseAsync<TResponse>(response, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return result;
            }

            if (IsBusinessResponse(result))
            {
                return result;
            }

            throw new PavoRestClientException(
                $"HTTP_{(int)response.StatusCode}",
                BuildHttpErrorMessage(response.StatusCode, result));
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new PavoRestClientException("TIMEOUT", $"PAVO isteği zaman aşımına uğradı ({client.Timeout.TotalSeconds:0}s).", ex);
        }
        catch (HttpRequestException ex) when (ex.InnerException is AuthenticationException or IOException)
        {
            throw new PavoRestClientException("TLS_CERTIFICATE", $"PAVO TLS/sertifika hatası: {ex.Message}", ex);
        }
        catch (HttpRequestException ex) when (ex.InnerException is SocketException socketEx)
        {
            throw new PavoRestClientException(MapSocketError(socketEx), MapSocketErrorMessage(socketEx), ex);
        }
        catch (HttpRequestException ex)
        {
            throw new PavoRestClientException("NETWORK", $"PAVO bağlantı hatası: {ex.Message}", ex);
        }
    }

    private static void ValidateRequest(PavoDeviceRequestBase request)
    {
        if (string.IsNullOrWhiteSpace(request.IpAddress))
            throw new PavoRestClientException("INVALID_REQUEST", "PAVO cihaz IP adresi boş olamaz.");
    }

    private static Uri BuildBaseUri(PavoDeviceRequestBase request)
    {
        var scheme = request.UseHttps || request.HttpsPort.HasValue ? "https" : "http";
        var port = request.UseHttps || request.HttpsPort.HasValue
            ? request.HttpsPort ?? 4568
            : request.HttpPort ?? 4567;

        var builder = new UriBuilder(scheme, request.IpAddress, port);
        return builder.Uri;
    }

    private static async Task<TResponse> ReadResponseAsync<TResponse>(HttpResponseMessage response, CancellationToken cancellationToken)
        where TResponse : PavoBaseResponse, new()
    {
        if (response.Content is null)
        {
            return new TResponse { HasError = !response.IsSuccessStatusCode, Message = response.ReasonPhrase };
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(content))
        {
            return new TResponse { HasError = !response.IsSuccessStatusCode, Message = response.ReasonPhrase };
        }

        try
        {
            var result = JsonSerializer.Deserialize<TResponse>(content, JsonOptions) ?? new TResponse();
            if (!response.IsSuccessStatusCode)
            {
                result.HasError = true;
                result.Message ??= response.ReasonPhrase;
            }

            return result;
        }
        catch (JsonException ex)
        {
            throw new PavoRestClientException("INVALID_RESPONSE", $"PAVO yanıtı ayrıştırılamadı: {ex.Message}", ex);
        }
    }

    private static PavoWirePairingRequest BuildPairingWireRequest(PavoPairingRequest request) => new()
    {
        TransactionHandle = request.TransactionHandle
    };

    private static PavoWireDeviceCommandRequest BuildPingWireRequest(PavoPingRequest request) => new()
    {
        TransactionHandle = request.TransactionHandle
    };

    private static PavoWireDeviceCommandRequest BuildGetDeviceInfoWireRequest(PavoGetDeviceInfoRequest request) => new()
    {
        TransactionHandle = request.TransactionHandle
    };

    private static PavoWireStartPaymentRequest BuildStartPaymentWireRequest(PavoStartPaymentRequest request)
    {
        var saleReference = request.SaleReference?.Trim() ?? string.Empty;
        var amount = request.Amount;
        var selectedSlots = request.SelectedSlots?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToArray();
        var selectedTerminals = request.SelectedTerminals?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToArray();
        var description = request.Description?.Trim();
        return new PavoWireStartPaymentRequest
        {
            TransactionHandle = request.TransactionHandle,
            Payment = new PavoWirePaymentRequest
            {
                Amount = amount,
                InstallmentCount = Math.Max(0, request.InstallmentCount),
                MinInstallmentCount = request.MinInstallmentCount,
                MaxInstallmentCount = request.MaxInstallmentCount,
                IsPfInstallmentEnabled = request.IsPfInstallmentEnabled,
                Puan = request.Puan,
                CurrencyCode = string.IsNullOrWhiteSpace(request.CurrencyCode) ? "TRY" : request.CurrencyCode.Trim(),
                SaleReference = saleReference,
                SelectedSlots = selectedSlots is { Length: > 0 } ? selectedSlots : ["rf", "icc", "magneticStripe", "qr", "manual"],
                CardReadTimeout = request.CardReadTimeout > 0 ? request.CardReadTimeout : 60,
                AllowDismissCardRead = request.AllowDismissCardRead,
                PinEntryTimeout = request.PinEntryTimeout > 0 ? request.PinEntryTimeout : 30,
                SelectedTerminals = selectedTerminals is { Length: > 0 } ? selectedTerminals : null,
                CustomApp = request.CustomApp,
                CustomLogin = request.CustomLogin,
                CustomCommission = request.CustomCommission,
                AdditionalInfo = new PavoWireAdditionalInfoRequest
                {
                    Print = request.PrintReceipt,
                    IsResponseBeforePrintEnabled = request.ResponseBeforePrintEnabled,
                    IsCustomerReceiptPrintEnabled = request.CustomerReceiptPrintEnabled,
                    IsMerchantReceiptPrintEnabled = request.MerchantReceiptPrintEnabled,
                    ReceiptImage = request.ReceiptImage,
                    CustomerReceiptImageEnabled = request.CustomerReceiptImageEnabled,
                    MerchantReceiptImageEnabled = request.MerchantReceiptImageEnabled,
                    ReceiptWidth = string.IsNullOrWhiteSpace(request.ReceiptWidth) ? "58mm" : request.ReceiptWidth.Trim(),
                    HeadUnmaskLength = request.HeadUnmaskLength,
                    TailUnmaskLength = request.TailUnmaskLength,
                    PrintData = new PavoWirePrintDataRequest
                    {
                        ReceiptJsonEnabled = request.ReceiptJsonEnabled,
                        CustomerReceiptJsonEnabled = request.CustomerReceiptJsonEnabled,
                        MerchantReceiptJsonEnabled = request.MerchantReceiptJsonEnabled,
                        ReceiptTextEnabled = request.ReceiptTextEnabled,
                        ReceiptTextWidth = string.IsNullOrWhiteSpace(request.ReceiptTextWidth) ? "40" : request.ReceiptTextWidth.Trim(),
                        CustomerReceiptTextEnabled = request.CustomerReceiptTextEnabled,
                        CustomerReceiptTextWidth = string.IsNullOrWhiteSpace(request.CustomerReceiptTextWidth) ? "40" : request.CustomerReceiptTextWidth.Trim(),
                        MerchantReceiptTextEnabled = request.MerchantReceiptTextEnabled,
                        MerchantReceiptTextWidth = string.IsNullOrWhiteSpace(request.MerchantReceiptTextWidth) ? "40" : request.MerchantReceiptTextWidth.Trim()
                    },
                    Header = description,
                    Footer = request.ReceiptFooter,
                    QrCodeText = saleReference,
                    List = BuildReceiptList(saleReference, amount, description)
                }
            }
        };
    }

    private static IReadOnlyList<PavoWireReceiptListItemRequest> BuildReceiptList(string saleReference, decimal amount, string? description)
    {
        var items = new List<PavoWireReceiptListItemRequest>
        {
            new() { Name = "İşlem referansı:", Value = saleReference },
            new() { Name = "Ödeme tutarı:", Value = $"{amount:N2} TL" },
            new() { Name = "İşlem tarihi:", Value = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) }
        };

        if (!string.IsNullOrWhiteSpace(description))
        {
            items.Add(new PavoWireReceiptListItemRequest { Name = "Açıklama:", Value = description });
        }

        return items;
    }

    private static PavoWireGetPaymentResultRequest BuildGetPaymentResultWireRequest(PavoGetPaymentResultRequest request) => new()
    {
        TransactionHandle = request.TransactionHandle
    };

    private static bool IsBusinessResponse(PavoBaseResponse response) =>
        response.HasError || response.HasAbondon || !string.IsNullOrWhiteSpace(response.ErrorCode);

    private static string BuildHttpErrorMessage(HttpStatusCode statusCode, PavoBaseResponse response)
    {
        var parts = new List<string> { $"PAVO HTTP {(int)statusCode}" };
        if (!string.IsNullOrWhiteSpace(response.ErrorCode))
            parts.Add(response.ErrorCode!);
        if (!string.IsNullOrWhiteSpace(response.Message))
            parts.Add(response.Message!);
        return string.Join(" - ", parts);
    }

    private static string MapSocketError(SocketException ex) => ex.SocketErrorCode switch
    {
        SocketError.ConnectionRefused => "CONNECTION_REFUSED",
        SocketError.HostUnreachable or SocketError.NetworkUnreachable => "NETWORK_UNREACHABLE",
        SocketError.TimedOut => "TIMEOUT",
        _ => "NETWORK"
    };

    private static string MapSocketErrorMessage(SocketException ex) => ex.SocketErrorCode switch
    {
        SocketError.ConnectionRefused => "PAVO bağlantısı reddedildi.",
        SocketError.HostUnreachable or SocketError.NetworkUnreachable => "PAVO ağına erişilemiyor.",
        SocketError.TimedOut => "PAVO bağlantısı zaman aşımına uğradı.",
        _ => $"PAVO ağ hatası: {ex.Message}"
    };
}
