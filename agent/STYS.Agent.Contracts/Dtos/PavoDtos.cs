using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace STYS.Agent.Contracts.Dtos;

public sealed class PavoTransactionHandle
{
    [JsonPropertyName("SerialNumber")]
    public string SerialNumber { get; set; } = string.Empty;
    [JsonPropertyName("Fingerprint")]
    public string Fingerprint { get; set; } = string.Empty;
    [JsonPropertyName("TransactionSequence")]
    public long TransactionSequence { get; set; }

    [JsonPropertyName("TransactionDate")]
    [JsonConverter(typeof(PavoTransactionDateJsonConverter))]
    public DateTime TransactionDate { get; set; }
}

internal sealed class PavoTransactionDateJsonConverter : JsonConverter<DateTime>
{
    private const string Format = "yyyy-MM-dd'T'HH:mm:ss.ffffff";

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            return default;
        }

        if (DateTime.TryParseExact(value, Format, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed))
        {
            return DateTime.SpecifyKind(parsed, DateTimeKind.Local);
        }

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out parsed))
        {
            return DateTime.SpecifyKind(parsed, DateTimeKind.Local);
        }

        throw new JsonException($"Geçersiz PAVO TransactionDate değeri: {value}");
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        var local = value.Kind == DateTimeKind.Utc ? value.ToLocalTime() : value;
        writer.WriteStringValue(local.ToString(Format, CultureInfo.InvariantCulture));
    }
}

public abstract class PavoDeviceRequestBase
{
    public int PosCihaziId { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public int? HttpPort { get; set; }
    public int? HttpsPort { get; set; }
    public bool UseHttps { get; set; }
    public PavoTransactionHandle TransactionHandle { get; set; } = new();
}

public sealed class PavoPairingRequest : PavoDeviceRequestBase
{
    public string? CurrentFingerprint { get; set; }
}

public sealed class PavoPingRequest : PavoDeviceRequestBase
{
}

public sealed class PavoGetDeviceInfoRequest : PavoDeviceRequestBase
{
    [JsonPropertyName("DeviceInfo")]
    public PavoGetDeviceInfoRequestDeviceInfo DeviceInfo { get; set; } = new();
}

public sealed class PavoGetDeviceInfoRequestDeviceInfo
{
    [JsonPropertyName("AdditionalInfo")]
    public PavoGetDeviceInfoRequestAdditionalInfo AdditionalInfo { get; set; } = new();
}

public sealed class PavoGetDeviceInfoRequestAdditionalInfo
{
    [JsonPropertyName("serialNumber")]
    public bool SerialNumber { get; set; } = true;

    [JsonPropertyName("fingerPrint")]
    public bool FingerPrint { get; set; } = true;

    [JsonPropertyName("appVersion")]
    public bool AppVersion { get; set; } = true;

    [JsonPropertyName("listTerminals")]
    public bool ListTerminals { get; set; } = true;
}

/// <summary>Safe projection of the PAVO wire payment data. Field names and types mirror the
/// reference contract exactly (see PavoWirePaymentResponseData), except that <c>cardNo</c> is
/// deliberately absent: it is sensitive card data, dropped at the wire-to-domain boundary and
/// never logged, persisted, or forwarded to the backend/frontend.</summary>
public sealed class PavoPaymentOperationData
{
    public long? Id { get; set; }
    public long? TransactionNo { get; set; }
    public long? BatchNo { get; set; }
    public bool IsSuccessful { get; set; }
    public string? StatusText { get; set; }
    public string? SaleReference { get; set; }
    public decimal? Amount { get; set; }
    public string? CurrencyCode { get; set; }
    public string? CardReaderSlotText { get; set; }
    public string? ResponseCode { get; set; }
    public string? AcquirerName { get; set; }
    public string? RetrievalReferenceNo { get; set; }
    public string? AuthorizationCode { get; set; }
    public string? FailMessage { get; set; }
    public string? CevapAciklama { get; set; }
    public int? ResultStatus { get; set; }
    public string? ResultDate { get; set; }
    public string? Terminal { get; set; }
    public string? CustomerReceiptImage { get; set; }
    public string? MerchantReceiptImage { get; set; }
    public string? ErrorReceiptImage { get; set; }
    public string? GunSonu { get; set; }
    public JsonElement? EodData { get; set; }
    public JsonElement? EodJson { get; set; }
    public JsonElement? EodText { get; set; }
    public string? EodImage { get; set; }
    public string? Reboot { get; set; }
    public string? EnterPinModeMessage { get; set; }
    public string? ExitPinModeMessage { get; set; }

    // STYS-only reconciliation fields. Not part of the PAVO wire contract - they are populated by
    // STYS payment bookkeeping, never by the device, and exist so existing backend reconciliation
    // logic keeps compiling and working.
    public bool IsPending { get; set; }
    public bool IsUnknown { get; set; }
    public string? ResultCode { get; set; }
    public string? Message { get; set; }
    public string? AcquirerReference { get; set; }
    public string? AcquirerId { get; set; }
    public string? TerminalId { get; set; }
    public string? MerchantId { get; set; }
    public string? TransactionStatus { get; set; }
}

/// <summary>Payment defaults taken verbatim from the reference PavoOptions.</summary>
public static class PavoPaymentDefaults
{
    public static readonly IReadOnlyList<string> SelectedSlots = ["rf", "icc", "magneticStripe", "qr", "manual"];
    public const string ReceiptHeader = "ÖDEME BİLGİSİ";
    public const string ReceiptFooter = "İyi günler dileriz.";
    public const string ReceiptWidth = "58mm";
    public const string ReceiptTextWidth = "40";
    public const int CardReadTimeoutSeconds = 60;
    public const int PinEntryTimeoutSeconds = 30;
    public const int HeadUnmaskLength = 4;
    public const int TailUnmaskLength = 4;
}

public abstract class PavoPaymentRequestBase : PavoDeviceRequestBase
{
    public int PosOdemeIslemiId { get; set; }
    public int PosTerminalId { get; set; }
    public string SaleReference { get; set; } = string.Empty;
}

public class PavoStartPaymentRequest : PavoPaymentRequestBase
{
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = "TRY";
    public string? Description { get; set; }
    public int InstallmentCount { get; set; }
    public int? MinInstallmentCount { get; set; }
    public int? MaxInstallmentCount { get; set; }
    public bool IsPfInstallmentEnabled { get; set; }
    public decimal Puan { get; set; }
    /// <summary>Defaults to the reference PavoOptions.SelectedSlots list.</summary>
    public IReadOnlyList<string>? SelectedSlots { get; set; } = PavoPaymentDefaults.SelectedSlots;
    public int CardReadTimeout { get; set; } = 60;
    public bool AllowDismissCardRead { get; set; } = true;
    public int PinEntryTimeout { get; set; } = 30;
    /// <summary>Reference default is an empty selection, which the wire builder sends as null.</summary>
    public IReadOnlyList<string>? SelectedTerminals { get; set; }
    public string? CustomApp { get; set; }
    public string? CustomLogin { get; set; }
    public decimal? CustomCommission { get; set; }
    // Defaults below mirror the reference PavoOptions defaults so that an unspecified STYS
    // payment behaves exactly like the reference client.
    public bool PrintReceipt { get; set; } = true;
    public bool ResponseBeforePrintEnabled { get; set; }
    public bool CustomerReceiptPrintEnabled { get; set; } = true;
    public bool MerchantReceiptPrintEnabled { get; set; } = true;
    public bool ReceiptImage { get; set; } = true;
    public bool CustomerReceiptImageEnabled { get; set; } = true;
    public bool MerchantReceiptImageEnabled { get; set; } = true;
    public string ReceiptWidth { get; set; } = "58mm";
    public int HeadUnmaskLength { get; set; } = 4;
    public int TailUnmaskLength { get; set; } = 4;
    public string? ReceiptHeader { get; set; } = "ÖDEME BİLGİSİ";
    public string? ReceiptFooter { get; set; } = "İyi günler dileriz.";
    public bool ReceiptJsonEnabled { get; set; } = true;
    public bool CustomerReceiptJsonEnabled { get; set; } = true;
    public bool MerchantReceiptJsonEnabled { get; set; } = true;
    public bool ReceiptTextEnabled { get; set; } = true;
    public string ReceiptTextWidth { get; set; } = "40";
    public bool CustomerReceiptTextEnabled { get; set; } = true;
    public string CustomerReceiptTextWidth { get; set; } = "40";
    public bool MerchantReceiptTextEnabled { get; set; } = true;
    public string MerchantReceiptTextWidth { get; set; } = "40";
}

public class PavoGetPaymentResultRequest : PavoPaymentRequestBase
{
    /// <summary>Receipt image request policy for the recovery query. Defaults request both customer
    /// and merchant receipt images (and accept the error receipt image on the response side).</summary>
    public PavoReceiptRequestOptions ReceiptOptions { get; set; } = new();
}

/// <summary>
/// Receipt image request options carried by GetPaymentResult. Kept as a dedicated domain object so
/// the recovery query's receipt policy is explicit rather than relying on agent-side defaults. This
/// is a STYS domain concept and must never be serialized as part of the PAVO wire contract directly;
/// the wire mapper projects it into the exact PAVO AdditionalInfo shape.
/// </summary>
public sealed class PavoReceiptRequestOptions
{
    public bool ReceiptImage { get; set; } = true;
    public bool CustomerReceiptImageEnabled { get; set; } = true;
    public bool MerchantReceiptImageEnabled { get; set; } = true;
    public string ReceiptWidth { get; set; } = "58mm";
    public int HeadUnmaskLength { get; set; }
    public int TailUnmaskLength { get; set; } = 4;
    public bool ReceiptJsonEnabled { get; set; }
    public bool CustomerReceiptJsonEnabled { get; set; }
    public bool MerchantReceiptJsonEnabled { get; set; }
    public bool ReceiptTextEnabled { get; set; }
    public string ReceiptTextWidth { get; set; } = "40";
    public bool CustomerReceiptTextEnabled { get; set; }
    public string CustomerReceiptTextWidth { get; set; } = "40";
    public bool MerchantReceiptTextEnabled { get; set; }
    public string MerchantReceiptTextWidth { get; set; } = "40";
}

public class PavoPerformEodRequest : PavoDeviceRequestBase
{
    public bool UseSummary { get; set; } = true;
    public bool Print { get; set; }
    public bool ReceiptImage { get; set; } = true;
}

public sealed class PavoRebootDeviceRequest : PavoDeviceRequestBase
{
}

public sealed class PavoEnterPinModeRequest : PavoDeviceRequestBase
{
}

public sealed class PavoExitPinModeRequest : PavoDeviceRequestBase
{
}

public abstract class PavoBaseResponse
{
    public bool HasError { get; set; }
    [JsonPropertyName("HasAbondon")]
    public bool HasAbondon { get; set; }
    public int? ErrorCode { get; set; }
    public string? Message { get; set; }
    /// <summary>Nullable to preserve the reference distinction between "Errors was absent from the
    /// response" and "Errors was an empty array".</summary>
    public List<string>? Errors { get; set; }

    /// <summary>Internal transport metadata. This is not serialized and does not belong to the
    /// PAVO wire contract.</summary>
    [JsonIgnore]
    public bool HttpSuccess { get; set; } = true;

    /// <summary>True when the HTTP response was actually received, even if the body later failed
    /// to read or parse.</summary>
    [JsonIgnore]
    public bool HttpResponseReceived { get; set; } = true;
}

public static class PavoResponseHelpers
{
    /// <summary>Reference (Pavo509.Client) common success semantics: !HasError &amp;&amp; !HasAbondon &amp;&amp;
    /// (ErrorCode == null || ErrorCode == 0). Applies to Pairing, PerformEOD, RebootDevice,
    /// EnterPinMode and ExitPinMode. The caller is responsible for also having received a valid,
    /// non-null response over a 2xx HTTP status.</summary>
    public static bool IsSuccessful(PavoBaseResponse response) =>
        !response.HasError && !response.HasAbondon && (response.ErrorCode is null || response.ErrorCode == 0);

    /// <summary>Transport-aware operation success: the PAVO envelope must be successful and the
    /// HTTP status must be 2xx.</summary>
    public static bool IsOperationSuccessful(PavoBaseResponse response) =>
        response.HttpSuccess && IsSuccessful(response);

    /// <summary>Reference StartPayment success semantics: common success AND Data is present AND
    /// Data.IsSuccessful. A payment that merely returns a clean envelope is NOT a successful payment.</summary>
    public static bool IsPaymentSuccessful(PavoPaymentResponseBase response) =>
        IsSuccessful(response) && response.Data is not null && response.Data.IsSuccessful;

    /// <summary>Transport-aware payment success.</summary>
    public static bool IsPaymentOperationSuccessful(PavoPaymentResponseBase response) =>
        IsOperationSuccessful(response) && response.Data is not null && response.Data.IsSuccessful;
}

public sealed class PavoPairingResponse : PavoBaseResponse
{
    /// <summary>The device's response handle. Nullable because the reference contract allows it to be
    /// absent. This is remote/device metadata only: it must never feed the outgoing client
    /// fingerprint or the next outgoing request sequence.</summary>
    public PavoTransactionHandle? TransactionHandle { get; set; }
}

// ---------------------------------------------------------------------------------------
// STYS extensions: Ping, GetDeviceInfo and GetPaymentResult do NOT exist in the Pavo509.Client
// reference project. Their envelope reuses the reference response shape, but any field below
// that is not on PavoBaseResponse is a STYS-only concept and must never be treated as part of
// the verified reference contract.
// ---------------------------------------------------------------------------------------

public sealed class PavoPingResponse : PavoBaseResponse
{
    public PavoTransactionHandle? TransactionHandle { get; set; }
    public string? DeviceTime { get; set; }
}

public sealed class PavoGetDeviceInfoResponse : PavoBaseResponse
{
    [JsonPropertyName("TransactionHandle")]
    public PavoTransactionHandle? TransactionHandle { get; set; }

    [JsonPropertyName("Data")]
    public PavoGetDeviceInfoResponseData? Data { get; set; }

    [JsonIgnore]
    public string? DeviceName
    {
        get => Data?.DeviceName;
        set
        {
            EnsureData().DeviceName = value;
        }
    }

    [JsonIgnore]
    public string? SerialNumber
    {
        get => Data?.SerialNumber;
        set
        {
            EnsureData().SerialNumber = value;
        }
    }

    [JsonIgnore]
    public string? Fingerprint
    {
        get => Data?.FingerPrint;
        set
        {
            EnsureData().FingerPrint = value;
        }
    }

    [JsonIgnore]
    public string? TargetFingerprint
    {
        get => Data?.FingerPrint;
        set
        {
            EnsureData().FingerPrint = value;
        }
    }

    [JsonIgnore]
    public List<PavoDeviceTerminalInfo> Terminals
    {
        get => Data?.ListTerminals ?? [];
        set
        {
            EnsureData().ListTerminals = value ?? [];
        }
    }

    private PavoGetDeviceInfoResponseData EnsureData() => Data ??= new PavoGetDeviceInfoResponseData();
}

public sealed class PavoGetDeviceInfoResponseData
{
    [JsonPropertyName("serialNumber")]
    public string? SerialNumber { get; set; }

    [JsonPropertyName("deviceName")]
    public string? DeviceName { get; set; }

    [JsonPropertyName("fingerPrint")]
    public string? FingerPrint { get; set; }

    [JsonPropertyName("appVersion")]
    public string? AppVersion { get; set; }

    [JsonPropertyName("defaultAcquirerId")]
    public int? DefaultAcquirerId { get; set; }

    [JsonPropertyName("listTerminals")]
    public List<PavoDeviceTerminalInfo> ListTerminals { get; set; } = [];
}

public sealed class PavoDeviceTerminalInfo
{
    [JsonPropertyName("acquirerId")]
    [JsonConverter(typeof(PavoFlexibleStringJsonConverter))]
    public string? AcquirerId { get; set; }

    [JsonPropertyName("acquirerName")]
    public string? AcquirerName { get; set; }

    [JsonPropertyName("terminalLabel")]
    public string? TerminalLabel { get; set; }

    [JsonPropertyName("terminalId")]
    public string TerminalId { get; set; } = string.Empty;

    [JsonPropertyName("merchantId")]
    public string? MerchantId { get; set; }

    [JsonPropertyName("isyeriSlipIsim")]
    public string? IsyeriSlipIsim { get; set; }

    [JsonPropertyName("isyeriSlipAdres")]
    public string? IsyeriSlipAdres { get; set; }

    [JsonPropertyName("loyaltyIndex")]
    public int? LoyaltyIndex { get; set; }

    [JsonPropertyName("availableCurrencyList")]
    public List<string>? AvailableCurrencyList { get; set; }
}

internal sealed class PavoFlexibleStringJsonConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.Null => null,
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number when reader.TryGetInt64(out var value) => value.ToString(CultureInfo.InvariantCulture),
            JsonTokenType.Number => reader.GetDecimal().ToString(CultureInfo.InvariantCulture),
            _ => throw new JsonException($"Geçersiz string/number değeri: {reader.TokenType}")
        };

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue))
        {
            writer.WriteNumberValue(longValue);
            return;
        }

        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var decimalValue) && value.Contains('.'))
        {
            writer.WriteNumberValue(decimalValue);
            return;
        }

        writer.WriteStringValue(value);
    }
}

public abstract class PavoPaymentResponseBase : PavoBaseResponse
{
    public PavoPaymentOperationData? Data { get; set; }
}

public sealed class PavoStartPaymentResponse : PavoPaymentResponseBase
{
}

public sealed class PavoGetPaymentResultResponse : PavoPaymentResponseBase
{
}

public sealed class PavoPerformEodResponse : PavoPaymentResponseBase
{
    /// <summary>The device's response handle. Its SerialNumber must match the request before an EOD
    /// is accepted as successful; the Fingerprint is remote/device identity and is not compared.</summary>
    public PavoTransactionHandle? TransactionHandle { get; set; }
}

public sealed class PavoRebootDeviceResponse : PavoPaymentResponseBase
{
}

public sealed class PavoEnterPinModeResponse : PavoPaymentResponseBase
{
}

public sealed class PavoExitPinModeResponse : PavoPaymentResponseBase
{
}
