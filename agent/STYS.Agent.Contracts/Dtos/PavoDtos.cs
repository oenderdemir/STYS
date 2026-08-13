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

public sealed class PavoPaymentOperationData
{
    public string? SaleReference { get; set; }
    public bool IsSuccessful { get; set; }
    public bool IsPending { get; set; }
    public bool IsUnknown { get; set; }
    public string? ResultCode { get; set; }
    public string? Message { get; set; }
    public string? RetrievalReferenceNo { get; set; }
    public string? AcquirerReference { get; set; }
    public string? AuthorizationCode { get; set; }
    public string? AcquirerId { get; set; }
    public string? TerminalId { get; set; }
    public string? MerchantId { get; set; }
    public string? TransactionStatus { get; set; }
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
    public IReadOnlyList<string>? SelectedSlots { get; set; }
    public int CardReadTimeout { get; set; } = 60;
    public bool AllowDismissCardRead { get; set; } = true;
    public int PinEntryTimeout { get; set; } = 30;
    public IReadOnlyList<string>? SelectedTerminals { get; set; }
    public string? CustomApp { get; set; }
    public string? CustomLogin { get; set; }
    public decimal? CustomCommission { get; set; }
    public bool PrintReceipt { get; set; }
    public bool ResponseBeforePrintEnabled { get; set; }
    public bool CustomerReceiptPrintEnabled { get; set; } = true;
    public bool MerchantReceiptPrintEnabled { get; set; } = true;
    public bool ReceiptImage { get; set; }
    public bool CustomerReceiptImageEnabled { get; set; }
    public bool MerchantReceiptImageEnabled { get; set; }
    public string ReceiptWidth { get; set; } = "58mm";
    public int HeadUnmaskLength { get; set; }
    public int TailUnmaskLength { get; set; } = 4;
    public string? ReceiptHeader { get; set; }
    public string? ReceiptFooter { get; set; }
    public bool ReceiptJsonEnabled { get; set; }
    public bool CustomerReceiptJsonEnabled { get; set; }
    public bool MerchantReceiptJsonEnabled { get; set; }
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

public abstract class PavoBaseResponse
{
    public bool HasError { get; set; }
    [JsonPropertyName("HasAbondon")]
    public bool HasAbondon { get; set; }
    public string? ErrorCode { get; set; }
    public string? Message { get; set; }
    public List<string> Errors { get; set; } = [];
}

public sealed class PavoPairingResponse : PavoBaseResponse
{
    public PavoTransactionHandle TransactionHandle { get; set; } = new();
    public string? Fingerprint { get; set; }
    public string? TargetFingerprint { get; set; }
    public long? PairingId { get; set; }
    public string? PairingCode { get; set; }
    public bool OnayliMi { get; set; }
}

public sealed class PavoPingResponse : PavoBaseResponse
{
    public PavoTransactionHandle TransactionHandle { get; set; } = new();
    public string? DeviceTime { get; set; }
}

public sealed class PavoGetDeviceInfoResponse : PavoBaseResponse
{
    public PavoTransactionHandle TransactionHandle { get; set; } = new();
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
