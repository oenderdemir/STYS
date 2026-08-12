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
