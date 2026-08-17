using System.Text.Json;
using System.Text.Json.Serialization;

namespace STYS.Agent.Modules.Pavo;

// =====================================================================================
// EXACT PAVO 509 WIRE CONTRACT.
//
// These types mirror the verified-working Pavo509.Client reference project 1:1 —
// property set, JSON names, nesting, CLR types, nullability and defaults. They are the
// only shapes ever serialized to / deserialized from the device.
//
// Rules for this file:
//   * Never add a STYS-specific field here. STYS domain concepts live in
//     STYS.Agent.Contracts.Dtos and are joined to these via an explicit mapper.
//   * Never "improve" a type (e.g. string -> DateTime). If the reference sends a
//     pre-formatted string, so do we.
//   * Every property carries an explicit [JsonPropertyName]; nothing relies on a
//     naming policy.
//
// Reference: Pavo509.Client/Models/*.cs
// =====================================================================================

internal sealed class PavoWireTransactionHandle
{
    [JsonPropertyName("SerialNumber")]
    public string SerialNumber { get; init; } = string.Empty;

    [JsonPropertyName("Fingerprint")]
    public string Fingerprint { get; init; } = string.Empty;

    [JsonPropertyName("TransactionSequence")]
    public int TransactionSequence { get; init; }

    [JsonPropertyName("TransactionDate")]
    public string TransactionDate { get; init; } = string.Empty;
}

internal sealed class PavoWirePairingRequest
{
    [JsonPropertyName("TransactionHandle")]
    public PavoWireTransactionHandle TransactionHandle { get; init; } = new();
}

internal sealed class PavoWireGetDeviceInfoRequest
{
    [JsonPropertyName("TransactionHandle")]
    public PavoWireTransactionHandle TransactionHandle { get; init; } = new();

    [JsonPropertyName("DeviceInfo")]
    public PavoWireGetDeviceInfoRequestDeviceInfo DeviceInfo { get; init; } = new();
}

internal sealed class PavoWireGetDeviceInfoRequestDeviceInfo
{
    [JsonPropertyName("AdditionalInfo")]
    public PavoWireGetDeviceInfoRequestAdditionalInfo AdditionalInfo { get; init; } = new();
}

internal sealed class PavoWireGetDeviceInfoRequestAdditionalInfo
{
    [JsonPropertyName("serialNumber")]
    public bool SerialNumber { get; init; } = true;

    [JsonPropertyName("fingerPrint")]
    public bool FingerPrint { get; init; } = true;

    [JsonPropertyName("appVersion")]
    public bool AppVersion { get; init; } = true;

    [JsonPropertyName("listTerminals")]
    public bool ListTerminals { get; init; } = true;
}

/// <summary>Shape used by /RebootDevice, /EnterPinMode and /ExitPinMode — all three send
/// nothing but the transaction handle.</summary>
internal sealed class PavoWireDeviceCommandRequest
{
    [JsonPropertyName("TransactionHandle")]
    public PavoWireTransactionHandle TransactionHandle { get; init; } = new();
}

internal sealed class PavoWireStartPaymentRequest
{
    [JsonPropertyName("TransactionHandle")]
    public PavoWireTransactionHandle TransactionHandle { get; init; } = new();

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

internal sealed class PavoWirePerformEodRequest
{
    [JsonPropertyName("PerformEOD")]
    public PavoWirePerformEodOperation PerformEod { get; init; } = new();

    [JsonPropertyName("TransactionHandle")]
    public PavoWireTransactionHandle TransactionHandle { get; init; } = new();
}

internal sealed class PavoWirePerformEodOperation
{
    [JsonPropertyName("AdditionalInfo")]
    public PavoWirePerformEodAdditionalInfo AdditionalInfo { get; init; } = new();
}

internal sealed class PavoWirePerformEodAdditionalInfo
{
    [JsonPropertyName("print")]
    public bool Print { get; init; }

    [JsonPropertyName("receiptImage")]
    public bool ReceiptImage { get; init; }

    [JsonPropertyName("useSummary")]
    public bool UseSummary { get; init; } = true;

    [JsonPropertyName("receiptWidth")]
    public string ReceiptWidth { get; init; } = "58mm";

    [JsonPropertyName("printData")]
    public PavoWirePerformEodPrintData PrintData { get; init; } = new();
}

internal sealed class PavoWirePerformEodPrintData
{
    [JsonPropertyName("receiptJsonEnabled")]
    public bool ReceiptJsonEnabled { get; init; } = true;

    [JsonPropertyName("receiptTextEnabled")]
    public bool ReceiptTextEnabled { get; init; } = true;

    [JsonPropertyName("receiptTextWidth")]
    public string ReceiptTextWidth { get; init; } = "40";
}

// ------------------------------------- responses -------------------------------------

internal sealed class PavoWireResponse
{
    [JsonPropertyName("HasAbondon")]
    public bool HasAbondon { get; set; }

    [JsonPropertyName("HasError")]
    public bool HasError { get; set; }

    [JsonPropertyName("ErrorCode")]
    public int? ErrorCode { get; set; }

    [JsonPropertyName("Message")]
    public string? Message { get; set; }

    [JsonPropertyName("TransactionHandle")]
    public PavoWireTransactionHandle? TransactionHandle { get; set; }

    [JsonPropertyName("Errors")]
    public List<string>? Errors { get; set; }

    [JsonPropertyName("Data")]
    public PavoWirePaymentResponseData? Data { get; set; }
}

internal sealed class PavoWireGetDeviceInfoResponse
{
    [JsonPropertyName("HasAbondon")]
    public bool HasAbondon { get; set; }

    [JsonPropertyName("HasError")]
    public bool HasError { get; set; }

    [JsonPropertyName("ErrorCode")]
    public int? ErrorCode { get; set; }

    [JsonPropertyName("Message")]
    public string? Message { get; set; }

    [JsonPropertyName("TransactionHandle")]
    public PavoWireTransactionHandle? TransactionHandle { get; set; }

    [JsonPropertyName("Errors")]
    public List<string>? Errors { get; set; }

    [JsonPropertyName("Data")]
    public PavoWireGetDeviceInfoResponseData? Data { get; set; }
}

internal sealed class PavoWireGetDeviceInfoResponseData
{
    [JsonPropertyName("serialNumber")]
    public string? SerialNumber { get; set; }

    [JsonPropertyName("fingerPrint")]
    public string? FingerPrint { get; set; }

    [JsonPropertyName("appVersion")]
    public string? AppVersion { get; set; }

    [JsonPropertyName("defaultAcquirerId")]
    public int? DefaultAcquirerId { get; set; }

    [JsonPropertyName("listTerminals")]
    public List<PavoWireGetDeviceInfoTerminalInfo>? ListTerminals { get; set; }
}

internal sealed class PavoWireGetDeviceInfoTerminalInfo
{
    [JsonPropertyName("acquirerId")]
    public int? AcquirerId { get; set; }

    [JsonPropertyName("acquirerName")]
    public string? AcquirerName { get; set; }

    [JsonPropertyName("terminalLabel")]
    public string? TerminalLabel { get; set; }

    [JsonPropertyName("terminalId")]
    public string? TerminalId { get; set; }

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

internal sealed class PavoWirePaymentResponseData
{
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    [JsonPropertyName("transactionNo")]
    public long? TransactionNo { get; set; }

    [JsonPropertyName("batchNo")]
    public long? BatchNo { get; set; }

    [JsonPropertyName("isSuccessful")]
    public bool IsSuccessful { get; set; }

    [JsonPropertyName("statusText")]
    public string? StatusText { get; set; }

    [JsonPropertyName("saleReference")]
    public string? SaleReference { get; set; }

    [JsonPropertyName("amount")]
    public decimal? Amount { get; set; }

    [JsonPropertyName("currencyCode")]
    public string? CurrencyCode { get; set; }

    /// <summary>Present in the reference wire contract, so it is parsed here for exact parity.
    /// It is deliberately dropped at the wire-to-domain mapping boundary and must never be
    /// logged, persisted, or forwarded to the backend/frontend.</summary>
    [JsonPropertyName("cardNo")]
    public string? CardNo { get; set; }

    [JsonPropertyName("cardReaderSlotText")]
    public string? CardReaderSlotText { get; set; }

    [JsonPropertyName("responseCode")]
    public string? ResponseCode { get; set; }

    [JsonPropertyName("acquirerName")]
    public string? AcquirerName { get; set; }

    [JsonPropertyName("retrievalReferenceNo")]
    public string? RetrievalReferenceNo { get; set; }

    [JsonPropertyName("authorizationCode")]
    public string? AuthorizationCode { get; set; }

    [JsonPropertyName("failMessage")]
    public string? FailMessage { get; set; }

    [JsonPropertyName("cevapAciklama")]
    public string? CevapAciklama { get; set; }

    [JsonPropertyName("resultStatus")]
    public int? ResultStatus { get; set; }

    [JsonPropertyName("resultDate")]
    public string? ResultDate { get; set; }

    [JsonPropertyName("terminal")]
    public string? Terminal { get; set; }

    [JsonPropertyName("customerReceiptImage")]
    public string? CustomerReceiptImage { get; set; }

    [JsonPropertyName("merchantReceiptImage")]
    public string? MerchantReceiptImage { get; set; }

    [JsonPropertyName("gunSonu")]
    public string? GunSonu { get; set; }

    [JsonPropertyName("eodData")]
    public JsonElement? EodData { get; set; }

    [JsonPropertyName("eodJson")]
    public JsonElement? EodJson { get; set; }

    [JsonPropertyName("eodText")]
    public JsonElement? EodText { get; set; }

    [JsonPropertyName("eodImage")]
    public string? EodImage { get; set; }

    [JsonPropertyName("reboot")]
    public string? Reboot { get; set; }

    [JsonPropertyName("enterPinModeMessage")]
    public string? EnterPinModeMessage { get; set; }

    [JsonPropertyName("exitPinModeMessage")]
    public string? ExitPinModeMessage { get; set; }
}
