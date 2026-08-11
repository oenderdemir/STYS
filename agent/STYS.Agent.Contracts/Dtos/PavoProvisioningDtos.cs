namespace STYS.Agent.Contracts.Dtos;

public sealed class PavoDeviceProvisioningCandidateTerminal
{
    public string? AcquirerId { get; set; }
    public string? AcquirerName { get; set; }
    public string TerminalId { get; set; } = string.Empty;
    public string? MerchantId { get; set; }
    public string SourceReference { get; set; } = string.Empty;
    public bool Active { get; set; }
    public DateTimeOffset? LastDiscoveredAt { get; set; }
}

public sealed class PavoDeviceProvisioningCandidate
{
    public string LocalDeviceId { get; set; } = string.Empty;
    public string Provider { get; set; } = "PAVO";
    public string DisplayName { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int HttpPort { get; set; }
    public int HttpsPort { get; set; }
    public string Protocol { get; set; } = string.Empty;
    public string? SerialNumber { get; set; }
    public string? DeviceName { get; set; }
    public DateTimeOffset? PairedAt { get; set; }
    public int? TesisId { get; set; }
    public IReadOnlyCollection<PavoDeviceProvisioningCandidateTerminal> Terminals { get; set; } = [];
}
