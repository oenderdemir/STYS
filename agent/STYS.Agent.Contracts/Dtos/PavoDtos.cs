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
}

public sealed class PavoPerformEodRequest : PavoDeviceRequestBase
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
}

public static class PavoResponseHelpers
{
    /// <summary>Reference (Pavo509.Client) common success semantics: !HasError &amp;&amp; !HasAbondon &amp;&amp;
    /// (ErrorCode == null || ErrorCode == 0). Applies to Pairing, PerformEOD, RebootDevice,
    /// EnterPinMode and ExitPinMode. The caller is responsible for also having received a valid,
    /// non-null response over a 2xx HTTP status.</summary>
    public static bool IsSuccessful(PavoBaseResponse response) =>
        !response.HasError && !response.HasAbondon && (response.ErrorCode is null || response.ErrorCode == 0);

    /// <summary>Reference StartPayment success semantics: common success AND Data is present AND
    /// Data.IsSuccessful. A payment that merely returns a clean envelope is NOT a successful payment.</summary>
    public static bool IsPaymentSuccessful(PavoPaymentResponseBase response) =>
        IsSuccessful(response) && response.Data is not null && response.Data.IsSuccessful;
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
    public PavoTransactionHandle? TransactionHandle { get; set; }
    public string? DeviceName { get; set; }
    public string? SerialNumber { get; set; }
    public string? Fingerprint { get; set; }
    public string? TargetFingerprint { get; set; }
    public List<PavoDeviceTerminalInfo> Terminals { get; set; } = [];
}

public sealed class PavoDeviceTerminalInfo
{
    public string TerminalId { get; set; } = string.Empty;
    public string? MerchantId { get; set; }
    public string? AcquirerId { get; set; }
    public string? AcquirerName { get; set; }
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
