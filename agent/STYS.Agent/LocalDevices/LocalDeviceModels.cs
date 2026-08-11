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
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
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
