using STYS.Agent.Contracts.Dtos;

namespace STYS.Agent.LocalDevices;

public enum LocalDeviceType
{
    Pos = 0,
    Printer = 1
}

public enum LocalDeviceProvider
{
    Pavo = 0
}

public enum LocalDeviceProtocol
{
    Http = 0,
    Https = 1
}

public enum LocalDeviceConnectionStatus
{
    Unknown = 0,
    Connected = 1,
    Unreachable = 2,
    Timeout = 3,
    TlsError = 4,
    ProtocolError = 5
}

public enum LocalDevicePairingStatus
{
    NotPaired = 0,
    Paired = 1,
    Failed = 2
}

public enum LocalDeviceProvisioningStatus
{
    NotProvisioned = 0,
    Provisioned = 1,
    ReProvisionRequired = 2,
    Conflict = 3,
    Disabled = 4,
    Failed = 5
}

public sealed class LocalDevice
{
    public string Id { get; set; } = string.Empty;
    public LocalDeviceType DeviceType { get; set; } = LocalDeviceType.Pos;
    public LocalDeviceProvider Provider { get; set; } = LocalDeviceProvider.Pavo;
    public string DisplayName { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int HttpPort { get; set; } = 4567;
    public int HttpsPort { get; set; } = 4568;
    public LocalDeviceProtocol Protocol { get; set; } = LocalDeviceProtocol.Http;
    public string? SerialNumber { get; set; }
    public string? DeviceName { get; set; }
    public LocalDeviceConnectionStatus Status { get; set; } = LocalDeviceConnectionStatus.Unknown;
    public DateTimeOffset? LastConnectionTestAt { get; set; }
    public bool? LastConnectionSuccess { get; set; }
    public string? LastError { get; set; }
    public LocalDevicePairingStatus PairingStatus { get; set; } = LocalDevicePairingStatus.NotPaired;
    public DateTimeOffset? LastDeviceInfoAt { get; set; }
    public DateTimeOffset? LastPairingAttemptAt { get; set; }
    public DateTimeOffset? LastPairingAt { get; set; }
    public string? LastPairingError { get; set; }
    public int? CentralPosCihaziId { get; set; }
    public int? CentralAgentId { get; set; }
    public int? CentralTesisId { get; set; }
    public DateTimeOffset? LastProvisionedAt { get; set; }
    public LocalDeviceProvisioningStatus ProvisioningStatus { get; set; } = LocalDeviceProvisioningStatus.NotProvisioned;
    public LocalDeviceStysReconciliationStatus? StysReconciliationStatus { get; set; }
    public string? StysReconciliationMessage { get; set; }
    public DateTimeOffset? StysReconciliationCheckedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public enum LocalDeviceStysReconciliationStatus
{
    InSync = 0,
    ReProvisionRequired = 1,
    CentralMissing = 2,
    OwnershipConflict = 3,
    Disabled = 4
}

public sealed class LocalDeviceStysReconciliationResult
{
    public string DeviceId { get; set; } = string.Empty;
    public LocalDeviceStysReconciliationStatus Status { get; set; } = LocalDeviceStysReconciliationStatus.CentralMissing;
    public string Message { get; set; } = string.Empty;
    public int? CentralPosCihaziId { get; set; }
    public int? CentralAgentId { get; set; }
    public int? CentralTesisId { get; set; }
    public string? CentralSerialNumber { get; set; }
    public string? CentralProvider { get; set; }
    public bool? CentralActive { get; set; }
    public DateTimeOffset CheckedAt { get; set; }
}

public sealed class LocalDeviceUpsertRequest
{
    public string? Id { get; set; }
    public LocalDeviceType DeviceType { get; set; } = LocalDeviceType.Pos;
    public LocalDeviceProvider Provider { get; set; } = LocalDeviceProvider.Pavo;
    public string DisplayName { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int? HttpPort { get; set; }
    public int? HttpsPort { get; set; }
    public LocalDeviceProtocol Protocol { get; set; } = LocalDeviceProtocol.Http;
    public string? SerialNumber { get; set; }
}

public sealed class LocalDevicePairingRequest
{
    public bool ForceRePair { get; set; }
}

public sealed class LocalDeviceTestRequest
{
    public LocalDeviceType DeviceType { get; set; } = LocalDeviceType.Pos;
    public LocalDeviceProvider Provider { get; set; } = LocalDeviceProvider.Pavo;
    public string DisplayName { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int? HttpPort { get; set; }
    public int? HttpsPort { get; set; }
    public LocalDeviceProtocol Protocol { get; set; } = LocalDeviceProtocol.Http;
    public string? SerialNumber { get; set; }
}

public sealed class LocalDevicePaymentTestRequest
{
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = "TRY";
    public string? SaleReference { get; set; }
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
    public string? SelectedTerminalId { get; set; }
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

public sealed class LocalDevicePaymentTestResult
{
    public string DeviceId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Status { get; set; } = "Unknown";
    public string Message { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
    public string SaleReference { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = "TRY";
    public string? SelectedTerminalId { get; set; }
    public PavoPaymentOperationData? Data { get; set; }
    public PavoStartPaymentResponse? Response { get; set; }
    public DateTimeOffset TestedAt { get; set; }
}

public sealed class LocalDeviceConnectionTestResult
{
    public string DeviceId { get; set; } = string.Empty;
    public LocalDeviceConnectionStatus Status { get; set; } = LocalDeviceConnectionStatus.Unknown;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset TestedAt { get; set; }
}

public sealed class LocalDeviceTerminal
{
    public string Id { get; set; } = string.Empty;
    public string LocalDeviceId { get; set; } = string.Empty;
    public string? AcquirerId { get; set; }
    public string? AcquirerName { get; set; }
    public string TerminalId { get; set; } = string.Empty;
    public string? MerchantId { get; set; }
    public string SourceReference { get; set; } = string.Empty;
    public bool Active { get; set; } = true;
    public DateTimeOffset? LastDiscoveredAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
