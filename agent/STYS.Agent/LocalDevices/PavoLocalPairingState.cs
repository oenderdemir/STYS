namespace STYS.Agent.LocalDevices;

public sealed class PavoLocalPairingState
{
    public string DeviceId { get; set; } = string.Empty;
    public string? Fingerprint { get; set; }
    public string? TargetFingerprint { get; set; }
    public long TransactionSequence { get; set; }
    public LocalDevicePairingStatus PairingStatus { get; set; } = LocalDevicePairingStatus.NotPaired;
    public DateTimeOffset? PairingAt { get; set; }
    public DateTimeOffset? LastPairingAttemptAt { get; set; }
    public string? LastPairingError { get; set; }
    public DateTimeOffset? LastDeviceInfoAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
