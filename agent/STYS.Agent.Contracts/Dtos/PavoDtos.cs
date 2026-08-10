using System.Text.Json.Serialization;

namespace STYS.Agent.Contracts.Dtos;

public sealed class PavoTransactionHandle
{
    public string SerialNumber { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;
    public long TransactionSequence { get; set; }
    public DateTime TransactionDate { get; set; }
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
