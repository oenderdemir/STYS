using System.Globalization;
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
    // Mirrors the reference client's serializer configuration exactly.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private const string TransactionDateFormat = "yyyy-MM-dd'T'HH:mm:ss.ffffff";

    // Reference PavoOptions paths.
    private const string PairingPath = "/Pairing";
    private const string StartPaymentPath = "/StartPayment";
    private const string PerformEodPath = "/PerformEOD";
    private const string RebootDevicePath = "/RebootDevice";
    private const string EnterPinModePath = "/EnterPinMode";
    private const string ExitPinModePath = "/ExitPinMode";

    // STYS extensions - no reference counterpart.
    private const string PingPath = "/Ping";
    private const string GetDeviceInfoPath = "/GetDeviceInfo";
    private const string GetPaymentResultPath = "/GetPaymentResult";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PavoRestClient> _logger;

    public PavoRestClient(IHttpClientFactory httpClientFactory, ILogger<PavoRestClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public Task<PavoPairingResponse> PairingAsync(PavoPairingRequest request, CancellationToken cancellationToken) =>
        SendAsync<PavoPairingResponse>(PairingPath, request, new PavoWirePairingRequest
        {
            TransactionHandle = BuildWireHandle(request.TransactionHandle)
        }, cancellationToken);

    public Task<PavoStartPaymentResponse> StartPaymentAsync(PavoStartPaymentRequest request, CancellationToken cancellationToken)
    {
        ValidatePaymentRequest(request);
        return SendAsync<PavoStartPaymentResponse>(StartPaymentPath, request, BuildStartPaymentWireRequest(request), cancellationToken);
    }

    public Task<PavoPerformEodResponse> PerformEodAsync(PavoPerformEodRequest request, CancellationToken cancellationToken) =>
        SendAsync<PavoPerformEodResponse>(PerformEodPath, request, new PavoWirePerformEodRequest
        {
            TransactionHandle = BuildWireHandle(request.TransactionHandle),
            PerformEod = new PavoWirePerformEodOperation
            {
                AdditionalInfo = new PavoWirePerformEodAdditionalInfo
                {
                    UseSummary = request.UseSummary,
                    Print = request.Print,
                    ReceiptImage = request.ReceiptImage,
                    ReceiptWidth = "58mm",
                    PrintData = new PavoWirePerformEodPrintData
                    {
                        ReceiptJsonEnabled = true,
                        ReceiptTextEnabled = true,
                        ReceiptTextWidth = "40"
                    }
                }
            }
        }, cancellationToken);

    public Task<PavoRebootDeviceResponse> RebootDeviceAsync(PavoRebootDeviceRequest request, CancellationToken cancellationToken) =>
        SendAsync<PavoRebootDeviceResponse>(RebootDevicePath, request, BuildDeviceCommandWireRequest(request), cancellationToken);

    public Task<PavoEnterPinModeResponse> EnterPinModeAsync(PavoEnterPinModeRequest request, CancellationToken cancellationToken) =>
        SendAsync<PavoEnterPinModeResponse>(EnterPinModePath, request, BuildDeviceCommandWireRequest(request), cancellationToken);

    public Task<PavoExitPinModeResponse> ExitPinModeAsync(PavoExitPinModeRequest request, CancellationToken cancellationToken) =>
        SendAsync<PavoExitPinModeResponse>(ExitPinModePath, request, BuildDeviceCommandWireRequest(request), cancellationToken);

    public Task<PavoPingResponse> PingAsync(PavoPingRequest request, CancellationToken cancellationToken) =>
        SendAsync<PavoPingResponse>(PingPath, request, BuildDeviceCommandWireRequest(request), cancellationToken);

    public Task<PavoGetDeviceInfoResponse> GetDeviceInfoAsync(PavoGetDeviceInfoRequest request, CancellationToken cancellationToken) =>
        SendGetDeviceInfoAsync(request, cancellationToken);

    public Task<PavoGetPaymentResultResponse> GetPaymentResultAsync(PavoGetPaymentResultRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.SaleReference))
        {
            // Without it the device has nothing to match against and would answer about some other
            // transaction, or none. Failing here beats reconciling against the wrong payment.
            throw new PavoRestClientException(
                "INVALID_REQUEST",
                "PAVO ödeme sonucu sorgusu için SaleReference zorunludur.",
                httpResponseReceived: false);
        }

        return SendAsync<PavoGetPaymentResultResponse>(GetPaymentResultPath, request, new PavoWireGetPaymentResultRequest
        {
            PaymentResult = new PavoWirePaymentResultQuery { SaleReference = request.SaleReference.Trim() },
            TransactionHandle = BuildWireHandle(request.TransactionHandle)
        }, cancellationToken);
    }

    private async Task<TResponse> SendAsync<TResponse>(
        string path,
        PavoDeviceRequestBase request,
        object wireRequest,
        CancellationToken cancellationToken)
        where TResponse : PavoBaseResponse, new()
    {
        ValidateRequest(request);

        var client = _httpClientFactory.CreateClient("PavoClient");
        client.Timeout = client.Timeout == Timeout.InfiniteTimeSpan ? TimeSpan.FromSeconds(180) : client.Timeout;

        var baseUri = BuildBaseUri(request);
        var uri = new Uri(baseUri, path);

        _logger.LogDebug(
            "PAVO isteği gönderiliyor. Endpoint={Endpoint}, Scheme={Scheme}, Port={Port}, SerialNumber={SerialNumber}, Sequence={Sequence}, TransactionDate={TransactionDate}",
            path,
            uri.Scheme,
            uri.Port,
            request.TransactionHandle.SerialNumber,
            request.TransactionHandle.TransactionSequence,
            FormatTransactionDate(request.TransactionHandle.TransactionDate));

        HttpResponseMessage response;
        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, uri)
            {
                Content = JsonContent.Create(wireRequest, wireRequest.GetType(), options: JsonOptions)
            };

            response = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw BuildTimeoutException(ex, client.Timeout);
        }
        catch (HttpRequestException ex) when (ex.InnerException is AuthenticationException or IOException)
        {
            throw new PavoRestClientException("TLS_CERTIFICATE", $"PAVO TLS/sertifika hatası: {ex.Message}", httpResponseReceived: false, ex);
        }
        catch (HttpRequestException ex) when (ex.InnerException is SocketException socketEx)
        {
            throw new PavoRestClientException(MapSocketError(socketEx), MapSocketErrorMessage(socketEx), httpResponseReceived: false, ex);
        }
        catch (HttpRequestException ex)
        {
            throw new PavoRestClientException("NETWORK", $"PAVO bağlantı hatası: {ex.Message}", httpResponseReceived: false, ex);
        }

        using (response)
        {
            string body;
            try
            {
                body = response.Content is null ? string.Empty : await response.Content.ReadAsStringAsync(cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                throw new PavoRestClientException(
                    "BODY_READ_FAILED",
                    $"PAVO yanıt gövdesi okunamadı (HTTP {(int)response.StatusCode}).",
                    httpResponseReceived: true,
                    ex);
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException or OperationCanceledException)
            {
                throw new PavoRestClientException(
                    "BODY_READ_FAILED",
                    $"PAVO yanıt gövdesi okunamadı (HTTP {(int)response.StatusCode}).",
                    httpResponseReceived: true,
                    ex);
            }

            _logger.LogDebug(
                "PAVO yanıtı alındı. Endpoint={Endpoint}, HttpStatus={HttpStatus}, BodyLength={BodyLength}",
                path,
                (int)response.StatusCode,
                body.Length);

            // Reference semantics: an HTTP 2xx alone is NOT success. A valid, non-null device
            // response must have been parsed, so empty and malformed bodies are hard failures.
            if (string.IsNullOrWhiteSpace(body))
            {
                throw new PavoRestClientException(
                    response.IsSuccessStatusCode ? "EMPTY_RESPONSE" : $"HTTP_{(int)response.StatusCode}",
                    $"PAVO boş yanıt döndürdü (HTTP {(int)response.StatusCode}).",
                    httpResponseReceived: true);
            }

            PavoWireResponse? wireResponse;
            try
            {
                wireResponse = JsonSerializer.Deserialize<PavoWireResponse>(body, JsonOptions);
            }
            catch (JsonException ex)
            {
                throw new PavoRestClientException("INVALID_RESPONSE", $"PAVO yanıtı ayrıştırılamadı: {ex.Message}", httpResponseReceived: true, ex);
            }

            if (wireResponse is null)
            {
                throw new PavoRestClientException("INVALID_RESPONSE", "PAVO yanıtı boş bir nesneye ayrıştırıldı.", httpResponseReceived: true);
            }

            var result = MapToDomain<TResponse>(wireResponse);
            result.HttpResponseReceived = true;
            result.HttpSuccess = response.IsSuccessStatusCode;

            _logger.LogDebug(
                "PAVO yanıtı ayrıştırıldı. Endpoint={Endpoint}, HasError={HasError}, HasAbondon={HasAbondon}, ErrorCode={ErrorCode}",
                path,
                result.HasError,
                result.HasAbondon,
                result.ErrorCode);

            return result;
        }
    }

    private async Task<PavoGetDeviceInfoResponse> SendGetDeviceInfoAsync(
        PavoGetDeviceInfoRequest request,
        CancellationToken cancellationToken)
    {
        ValidateRequest(request);

        var client = _httpClientFactory.CreateClient("PavoClient");
        client.Timeout = client.Timeout == Timeout.InfiniteTimeSpan ? TimeSpan.FromSeconds(180) : client.Timeout;

        var baseUri = BuildBaseUri(request);
        var uri = new Uri(baseUri, GetDeviceInfoPath);

        _logger.LogDebug(
            "PAVO device info isteği gönderiliyor. Endpoint={Endpoint}, Scheme={Scheme}, Port={Port}, SerialNumber={SerialNumber}, Sequence={Sequence}, TransactionDate={TransactionDate}",
            GetDeviceInfoPath,
            uri.Scheme,
            uri.Port,
            request.TransactionHandle.SerialNumber,
            request.TransactionHandle.TransactionSequence,
            FormatTransactionDate(request.TransactionHandle.TransactionDate));

        var wireRequest = new PavoWireGetDeviceInfoRequest
        {
            TransactionHandle = BuildWireHandle(request.TransactionHandle),
            DeviceInfo = new PavoWireGetDeviceInfoRequestDeviceInfo
            {
                AdditionalInfo = new PavoWireGetDeviceInfoRequestAdditionalInfo
                {
                    SerialNumber = request.DeviceInfo.AdditionalInfo.SerialNumber,
                    FingerPrint = request.DeviceInfo.AdditionalInfo.FingerPrint,
                    AppVersion = request.DeviceInfo.AdditionalInfo.AppVersion,
                    ListTerminals = request.DeviceInfo.AdditionalInfo.ListTerminals
                }
            }
        };

        HttpResponseMessage response;
        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, uri)
            {
                Content = JsonContent.Create(wireRequest, wireRequest.GetType(), options: JsonOptions)
            };

            response = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw BuildTimeoutException(ex, client.Timeout);
        }
        catch (HttpRequestException ex) when (ex.InnerException is AuthenticationException or IOException)
        {
            throw new PavoRestClientException("TLS_CERTIFICATE", $"PAVO TLS/sertifika hatası: {ex.Message}", httpResponseReceived: false, ex);
        }
        catch (HttpRequestException ex) when (ex.InnerException is SocketException socketEx)
        {
            throw new PavoRestClientException(MapSocketError(socketEx), MapSocketErrorMessage(socketEx), httpResponseReceived: false, ex);
        }
        catch (HttpRequestException ex)
        {
            throw new PavoRestClientException("NETWORK", $"PAVO bağlantı hatası: {ex.Message}", httpResponseReceived: false, ex);
        }

        using (response)
        {
            string body;
            try
            {
                body = response.Content is null ? string.Empty : await response.Content.ReadAsStringAsync(cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                throw new PavoRestClientException(
                    "BODY_READ_FAILED",
                    $"PAVO yanıt gövdesi okunamadı (HTTP {(int)response.StatusCode}).",
                    httpResponseReceived: true,
                    ex);
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException or OperationCanceledException)
            {
                throw new PavoRestClientException(
                    "BODY_READ_FAILED",
                    $"PAVO yanıt gövdesi okunamadı (HTTP {(int)response.StatusCode}).",
                    httpResponseReceived: true,
                    ex);
            }

            _logger.LogDebug(
                "PAVO device info yanıtı alındı. Endpoint={Endpoint}, HttpStatus={HttpStatus}, BodyLength={BodyLength}",
                GetDeviceInfoPath,
                (int)response.StatusCode,
                body.Length);

            if (string.IsNullOrWhiteSpace(body))
            {
                throw new PavoRestClientException(
                    response.IsSuccessStatusCode ? "EMPTY_RESPONSE" : $"HTTP_{(int)response.StatusCode}",
                    $"PAVO boş yanıt döndürdü (HTTP {(int)response.StatusCode}).",
                    httpResponseReceived: true);
            }

            PavoWireGetDeviceInfoResponse? wireResponse;
            try
            {
                wireResponse = JsonSerializer.Deserialize<PavoWireGetDeviceInfoResponse>(body, JsonOptions);
            }
            catch (JsonException ex)
            {
                throw new PavoRestClientException("INVALID_RESPONSE", $"PAVO yanıtı ayrıştırılamadı: {ex.Message}", httpResponseReceived: true, ex);
            }

            if (wireResponse is null)
            {
                throw new PavoRestClientException("INVALID_RESPONSE", "PAVO yanıtı boş bir nesneye ayrıştırıldı.", httpResponseReceived: true);
            }

            var result = MapGetDeviceInfoToDomain(wireResponse);
            result.HttpResponseReceived = true;
            result.HttpSuccess = response.IsSuccessStatusCode;

            _logger.LogDebug(
                "PAVO device info yanıtı ayrıştırıldı. Endpoint={Endpoint}, HasError={HasError}, HasAbondon={HasAbondon}, ErrorCode={ErrorCode}",
                GetDeviceInfoPath,
                result.HasError,
                result.HasAbondon,
                result.ErrorCode);

            return result;
        }
    }

    private static TResponse MapToDomain<TResponse>(PavoWireResponse wire)
        where TResponse : PavoBaseResponse, new()
    {
        var result = new TResponse
        {
            HasError = wire.HasError,
            HasAbondon = wire.HasAbondon,
            ErrorCode = wire.ErrorCode,
            Message = wire.Message,
            Errors = wire.Errors
        };

        var handle = MapHandleToDomain(wire.TransactionHandle);
        switch (result)
        {
            case PavoPairingResponse pairing:
                pairing.TransactionHandle = handle;
                break;
            case PavoPingResponse ping:
                ping.TransactionHandle = handle;
                break;
            case PavoGetDeviceInfoResponse deviceInfo:
                deviceInfo.TransactionHandle = handle;
                break;
        }

        if (result is PavoPaymentResponseBase payment)
        {
            payment.Data = MapPaymentDataToDomain(wire.Data);
        }

        return result;
    }

    private static PavoGetDeviceInfoResponse MapGetDeviceInfoToDomain(PavoWireGetDeviceInfoResponse wire)
    {
        var result = new PavoGetDeviceInfoResponse
        {
            HasError = wire.HasError,
            HasAbondon = wire.HasAbondon,
            ErrorCode = wire.ErrorCode,
            Message = wire.Message,
            Errors = wire.Errors,
            TransactionHandle = MapHandleToDomain(wire.TransactionHandle),
            Data = wire.Data is null
                ? null
                : new PavoGetDeviceInfoResponseData
                {
                    SerialNumber = wire.Data.SerialNumber,
                    FingerPrint = wire.Data.FingerPrint,
                    AppVersion = wire.Data.AppVersion,
                    DefaultAcquirerId = wire.Data.DefaultAcquirerId,
                    ListTerminals = wire.Data.ListTerminals?.Select(MapTerminalToDomain).ToList() ?? []
                }
        };

        return result;
    }

    private static PavoDeviceTerminalInfo MapTerminalToDomain(PavoWireGetDeviceInfoTerminalInfo terminal) => new()
    {
        AcquirerId = terminal.AcquirerId?.ToString(CultureInfo.InvariantCulture),
        AcquirerName = terminal.AcquirerName,
        TerminalLabel = terminal.TerminalLabel,
        TerminalId = terminal.TerminalId ?? string.Empty,
        MerchantId = terminal.MerchantId,
        IsyeriSlipIsim = terminal.IsyeriSlipIsim,
        IsyeriSlipAdres = terminal.IsyeriSlipAdres,
        LoyaltyIndex = terminal.LoyaltyIndex,
        AvailableCurrencyList = terminal.AvailableCurrencyList
    };

    private static PavoTransactionHandle? MapHandleToDomain(PavoWireTransactionHandle? handle)
    {
        if (handle is null)
        {
            return null;
        }

        return new PavoTransactionHandle
        {
            SerialNumber = handle.SerialNumber,
            Fingerprint = handle.Fingerprint,
            TransactionSequence = handle.TransactionSequence,
            TransactionDate = ParseTransactionDate(handle.TransactionDate)
        };
    }

    /// <summary>Wire-to-domain projection. <c>cardNo</c> is intentionally dropped here: it is
    /// sensitive card data that must not leave this boundary.</summary>
    private static PavoPaymentOperationData? MapPaymentDataToDomain(PavoWirePaymentResponseData? data)
    {
        if (data is null)
        {
            return null;
        }

        return new PavoPaymentOperationData
        {
            Id = data.Id,
            TransactionNo = data.TransactionNo,
            BatchNo = data.BatchNo,
            IsSuccessful = data.IsSuccessful,
            StatusText = data.StatusText,
            SaleReference = data.SaleReference,
            Amount = data.Amount,
            CurrencyCode = data.CurrencyCode,
            CardReaderSlotText = data.CardReaderSlotText,
            ResponseCode = data.ResponseCode,
            AcquirerName = data.AcquirerName,
            RetrievalReferenceNo = data.RetrievalReferenceNo,
            AuthorizationCode = data.AuthorizationCode,
            FailMessage = data.FailMessage,
            CevapAciklama = data.CevapAciklama,
            ResultStatus = data.ResultStatus,
            ResultDate = data.ResultDate,
            Terminal = data.Terminal,
            CustomerReceiptImage = data.CustomerReceiptImage,
            MerchantReceiptImage = data.MerchantReceiptImage,
            GunSonu = data.GunSonu,
            EodData = data.EodData,
            EodJson = data.EodJson,
            EodText = data.EodText,
            EodImage = data.EodImage,
            Reboot = data.Reboot,
            EnterPinModeMessage = data.EnterPinModeMessage,
            ExitPinModeMessage = data.ExitPinModeMessage
        };
    }

    /// <summary>Reference StartPayment validation. Invalid input is rejected outright rather than
    /// silently corrected, so STYS can never send a payment the reference client would refuse.</summary>
    private static void ValidatePaymentRequest(PavoStartPaymentRequest request)
    {
        if (request.Amount <= 0)
        {
            throw new PavoRestClientException("INVALID_REQUEST", "Ödeme tutarı sıfırdan büyük olmalıdır.", httpResponseReceived: false);
        }

        if (request.InstallmentCount == 1 || request.InstallmentCount < 0)
        {
            throw new PavoRestClientException("INVALID_REQUEST", "Taksit sayısı peşin için 0, taksitli işlem için en az 2 olmalıdır.", httpResponseReceived: false);
        }

        if (string.IsNullOrWhiteSpace(request.SaleReference))
        {
            throw new PavoRestClientException("INVALID_REQUEST", "Satış referansı boş olamaz.", httpResponseReceived: false);
        }
    }

    private static void ValidateRequest(PavoDeviceRequestBase request)
    {
        if (string.IsNullOrWhiteSpace(request.IpAddress))
            throw new PavoRestClientException("INVALID_REQUEST", "PAVO cihaz IP adresi boş olamaz.", httpResponseReceived: false);
    }

    private static Uri BuildBaseUri(PavoDeviceRequestBase request)
    {
        // UseHttps is the sole source of truth for scheme selection. A device may have an
        // HttpsPort on file without actually speaking HTTPS, so HttpsPort presence alone must
        // never flip an HTTP device over to HTTPS. Reference default is http on 4567.
        var scheme = request.UseHttps ? "https" : "http";
        var port = request.UseHttps
            ? request.HttpsPort ?? 4568
            : request.HttpPort ?? 4567;

        var builder = new UriBuilder(scheme, request.IpAddress, port);
        return builder.Uri;
    }

    private static PavoWireTransactionHandle BuildWireHandle(PavoTransactionHandle handle) => new()
    {
        SerialNumber = handle.SerialNumber,
        Fingerprint = handle.Fingerprint,
        TransactionSequence = ToWireSequence(handle.TransactionSequence),
        TransactionDate = FormatTransactionDate(handle.TransactionDate)
    };

    /// <summary>The wire contract is int; the STYS domain carries long for persistence headroom.</summary>
    private static int ToWireSequence(long sequence)
    {
        if (sequence is < int.MinValue or > int.MaxValue)
        {
            throw new PavoRestClientException(
                "INVALID_REQUEST",
                $"PAVO transaction sequence değeri wire contract sınırlarının dışında: {sequence}.",
                httpResponseReceived: false);
        }

        return (int)sequence;
    }

    private static string FormatTransactionDate(DateTime value)
    {
        var local = value.Kind == DateTimeKind.Utc ? value.ToLocalTime() : value;
        return local.ToString(TransactionDateFormat, CultureInfo.InvariantCulture);
    }

    private static DateTime ParseTransactionDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return default;
        }

        if (DateTime.TryParseExact(value, TransactionDateFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed)
            || DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out parsed))
        {
            return DateTime.SpecifyKind(parsed, DateTimeKind.Local);
        }

        return default;
    }

    private static PavoWireDeviceCommandRequest BuildDeviceCommandWireRequest(PavoDeviceRequestBase request) => new()
    {
        TransactionHandle = BuildWireHandle(request.TransactionHandle)
    };

    private static PavoWireStartPaymentRequest BuildStartPaymentWireRequest(PavoStartPaymentRequest request)
    {
        var saleReference = request.SaleReference.Trim();
        var amount = request.Amount;
        var selectedSlots = request.SelectedSlots?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToArray();
        var selectedTerminals = request.SelectedTerminals?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToArray();

        return new PavoWireStartPaymentRequest
        {
            TransactionHandle = BuildWireHandle(request.TransactionHandle),
            Payment = new PavoWirePaymentRequest
            {
                // Reference: Amount is sent in the major currency unit; never converted to minor units.
                Amount = amount,
                InstallmentCount = request.InstallmentCount,
                MinInstallmentCount = request.MinInstallmentCount,
                MaxInstallmentCount = request.MaxInstallmentCount,
                IsPfInstallmentEnabled = request.IsPfInstallmentEnabled,
                Puan = request.Puan,
                CurrencyCode = string.IsNullOrWhiteSpace(request.CurrencyCode) ? "TRY" : request.CurrencyCode.Trim(),
                SaleReference = saleReference,
                // Reference sends null (not an empty array) when no slots/terminals are selected.
                SelectedSlots = selectedSlots is { Length: > 0 } ? selectedSlots : null,
                CardReadTimeout = request.CardReadTimeout,
                AllowDismissCardRead = request.AllowDismissCardRead,
                PinEntryTimeout = request.PinEntryTimeout,
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
                    ReceiptWidth = request.ReceiptWidth,
                    HeadUnmaskLength = request.HeadUnmaskLength,
                    TailUnmaskLength = request.TailUnmaskLength,
                    PrintData = new PavoWirePrintDataRequest
                    {
                        ReceiptJsonEnabled = request.ReceiptJsonEnabled,
                        CustomerReceiptJsonEnabled = request.CustomerReceiptJsonEnabled,
                        MerchantReceiptJsonEnabled = request.MerchantReceiptJsonEnabled,
                        ReceiptTextEnabled = request.ReceiptTextEnabled,
                        ReceiptTextWidth = request.ReceiptTextWidth,
                        CustomerReceiptTextEnabled = request.CustomerReceiptTextEnabled,
                        CustomerReceiptTextWidth = request.CustomerReceiptTextWidth,
                        MerchantReceiptTextEnabled = request.MerchantReceiptTextEnabled,
                        MerchantReceiptTextWidth = request.MerchantReceiptTextWidth
                    },
                    Header = request.ReceiptHeader,
                    Footer = request.ReceiptFooter,
                    QrCodeText = saleReference,
                    List = BuildReceiptList(saleReference, amount)
                }
            }
        };
    }

    /// <summary>Reference receipt list: exactly these three rows, in this order.</summary>
    private static IReadOnlyList<PavoWireReceiptListItemRequest> BuildReceiptList(string saleReference, decimal amount) =>
    [
        new() { Name = "İşlem referansı:", Value = saleReference },
        new() { Name = "Ödeme tutarı:", Value = $"{amount:N2} TL" },
        new() { Name = "İşlem tarihi:", Value = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss") }
    ];

    /// <summary>
    /// Separates a connect-phase timeout from a response timeout.
    ///
    /// Both surface as TaskCanceledException wrapping a TimeoutException, so the outer type is not
    /// enough. The structural difference is that HttpClient.Timeout nests a further
    /// TaskCanceledException inside the TimeoutException, while SocketsHttpHandler.ConnectTimeout
    /// does not. That matters for payments: a connect timeout proves the device was never reached,
    /// whereas a response timeout leaves open that the card was charged.
    ///
    /// The nesting is a runtime implementation detail, so PavoTimeoutClassificationTests asserts it
    /// against real timeouts rather than trusting it blindly — if a future runtime changes shape,
    /// those tests fail instead of the classification silently regressing.
    /// </summary>
    private static PavoRestClientException BuildTimeoutException(TaskCanceledException ex, TimeSpan requestTimeout)
    {
        var connectPhase = ex.InnerException is TimeoutException timeout
            && timeout.InnerException is not TaskCanceledException;

        return connectPhase
            ? new PavoRestClientException(
                PavoDeviceReachability.ConnectTimeout,
                "PAVO cihazına bağlantı kurulamadı (bağlantı zaman aşımı). Cihaz kapalı veya ağda erişilemiyor olabilir.",
                httpResponseReceived: false,
                ex)
            : new PavoRestClientException(
                PavoDeviceReachability.ResponseTimeout,
                $"PAVO isteği zaman aşımına uğradı ({requestTimeout.TotalSeconds:0}s).",
                httpResponseReceived: false,
                ex);
    }

    // A SocketException here comes from the connect phase, so the device was never reached. The
    // timeout case is reported as CONNECT_TIMEOUT rather than TIMEOUT specifically to keep it
    // distinguishable from a request that was sent and went unanswered — only the latter leaves a
    // payment genuinely ambiguous. TIMEOUT remains reserved for that (TaskCanceledException) path.
    private static string MapSocketError(SocketException ex) => ex.SocketErrorCode switch
    {
        SocketError.ConnectionRefused => PavoDeviceReachability.ConnectionRefused,
        SocketError.HostUnreachable or SocketError.NetworkUnreachable => PavoDeviceReachability.NetworkUnreachable,
        SocketError.TimedOut => PavoDeviceReachability.ConnectTimeout,
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
