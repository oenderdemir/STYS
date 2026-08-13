using System.Text.Json;
using System.Text.Json.Serialization;
using STYS.Agent.Contracts.Dtos;

namespace STYS.Agent.Modules.Pavo;

internal abstract class PavoWireRequestBase
{
    [JsonIgnore]
    public int PosCihaziId { get; init; }

    [JsonIgnore]
    public string IpAddress { get; init; } = string.Empty;

    [JsonIgnore]
    public int? HttpPort { get; init; }

    [JsonIgnore]
    public int? HttpsPort { get; init; }

    [JsonIgnore]
    public bool UseHttps { get; init; }

    [JsonPropertyName("TransactionHandle")]
    public PavoTransactionHandle TransactionHandle { get; init; } = new();
}

internal sealed class PavoWirePairingRequest : PavoWireRequestBase
{
}

internal sealed class PavoWireDeviceCommandRequest : PavoWireRequestBase
{
}

internal sealed class PavoWireGetPaymentResultRequest : PavoWireRequestBase
{
}

internal sealed class PavoWireStartPaymentRequest : PavoWireRequestBase
{
    [JsonPropertyName("Payment")]
    public PavoWirePaymentRequest Payment { get; init; } = new();
}

internal sealed class PavoWirePaymentRequest
{
    [JsonPropertyName("Amount")]
    public decimal Amount { get; init; }

    [JsonPropertyName("InstallmentCount")]
    public int InstallmentCount { get; init; }

    [JsonPropertyName("MinInstallmentCount")]
    public int? MinInstallmentCount { get; init; }

    [JsonPropertyName("MaxInstallmentCount")]
    public int? MaxInstallmentCount { get; init; }

    [JsonPropertyName("IsPfInstallmentEnabled")]
    public bool IsPfInstallmentEnabled { get; init; }

    [JsonPropertyName("Puan")]
    public decimal Puan { get; init; }

    [JsonPropertyName("CurrencyCode")]
    public string CurrencyCode { get; init; } = "TRY";

    [JsonPropertyName("SaleReference")]
    public string SaleReference { get; init; } = string.Empty;

    [JsonPropertyName("SelectedSlots")]
    public IReadOnlyList<string>? SelectedSlots { get; init; }

    [JsonPropertyName("CardReadTimeout")]
    public int CardReadTimeout { get; init; } = 60;

    [JsonPropertyName("AllowDismissCardRead")]
    public bool AllowDismissCardRead { get; init; } = true;

    [JsonPropertyName("PinEntryTimeout")]
    public int PinEntryTimeout { get; init; } = 30;

    [JsonPropertyName("SelectedTerminals")]
    public IReadOnlyList<string>? SelectedTerminals { get; init; }

    [JsonPropertyName("CustomApp")]
    public string? CustomApp { get; init; }

    [JsonPropertyName("CustomLogin")]
    public string? CustomLogin { get; init; }

    [JsonPropertyName("CustomCommission")]
    public decimal? CustomCommission { get; init; }

    [JsonPropertyName("AdditionalInfo")]
    public PavoWireAdditionalInfoRequest AdditionalInfo { get; init; } = new();
}

internal sealed class PavoWireAdditionalInfoRequest
{
    [JsonPropertyName("print")]
    public bool Print { get; init; }

    [JsonPropertyName("isResponseBeforePrintEnabled")]
    public bool IsResponseBeforePrintEnabled { get; init; }

    [JsonPropertyName("isCustomerReceiptPrintEnabled")]
    public bool IsCustomerReceiptPrintEnabled { get; init; } = true;

    [JsonPropertyName("isMerchantReceiptPrintEnabled")]
    public bool IsMerchantReceiptPrintEnabled { get; init; } = true;

    [JsonPropertyName("receiptImage")]
    public bool ReceiptImage { get; init; }

    [JsonPropertyName("customerReceiptImageEnabled")]
    public bool CustomerReceiptImageEnabled { get; init; }

    [JsonPropertyName("merchantReceiptImageEnabled")]
    public bool MerchantReceiptImageEnabled { get; init; }

    [JsonPropertyName("receiptWidth")]
    public string ReceiptWidth { get; init; } = "58mm";

    [JsonPropertyName("headUnmaskLength")]
    public int HeadUnmaskLength { get; init; }

    [JsonPropertyName("tailUnmaskLength")]
    public int TailUnmaskLength { get; init; } = 4;

    [JsonPropertyName("printData")]
    public PavoWirePrintDataRequest PrintData { get; init; } = new();

    [JsonPropertyName("header")]
    public string? Header { get; init; }

    [JsonPropertyName("footer")]
    public string? Footer { get; init; }

    [JsonPropertyName("qrCodeText")]
    public string? QrCodeText { get; init; }

    [JsonPropertyName("list")]
    public IReadOnlyList<PavoWireReceiptListItemRequest>? List { get; init; }
}

internal sealed class PavoWirePrintDataRequest
{
    [JsonPropertyName("receiptJsonEnabled")]
    public bool ReceiptJsonEnabled { get; init; }

    [JsonPropertyName("customerReceiptJsonEnabled")]
    public bool CustomerReceiptJsonEnabled { get; init; }

    [JsonPropertyName("merchantReceiptJsonEnabled")]
    public bool MerchantReceiptJsonEnabled { get; init; }

    [JsonPropertyName("receiptTextEnabled")]
    public bool ReceiptTextEnabled { get; init; }

    [JsonPropertyName("receiptTextWidth")]
    public string ReceiptTextWidth { get; init; } = "40";

    [JsonPropertyName("customerReceiptTextEnabled")]
    public bool CustomerReceiptTextEnabled { get; init; }

    [JsonPropertyName("customerReceiptTextWidth")]
    public string CustomerReceiptTextWidth { get; init; } = "40";

    [JsonPropertyName("merchantReceiptTextEnabled")]
    public bool MerchantReceiptTextEnabled { get; init; }

    [JsonPropertyName("merchantReceiptTextWidth")]
    public string MerchantReceiptTextWidth { get; init; } = "40";
}

internal sealed class PavoWireReceiptListItemRequest
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("value")]
    public string Value { get; init; } = string.Empty;
}
