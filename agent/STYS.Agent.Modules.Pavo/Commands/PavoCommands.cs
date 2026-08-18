using STYS.Agent.Client.Commands;
using STYS.Agent.Contracts.Dtos;

namespace STYS.Agent.Modules.Pavo.Commands;

public abstract class PavoDeviceCommandBase : IAgentCommand
{
    public int PosCihaziId { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public int? HttpPort { get; set; }
    public int? HttpsPort { get; set; }
    public bool UseHttps { get; set; }
    public string? Fingerprint { get; set; }
    public PavoTransactionHandle TransactionHandle { get; set; } = new();

    public abstract string CommandType { get; }
}

public sealed class PavoPairingCommand : PavoDeviceCommandBase
{
    public string? CurrentFingerprint { get; set; }
    public override string CommandType => "PavoPairing";
}

public sealed class PavoPingCommand : PavoDeviceCommandBase
{
    public override string CommandType => "PavoPing";
}

public sealed class PavoGetDeviceInfoCommand : PavoDeviceCommandBase
{
    public override string CommandType => "PavoGetDeviceInfo";
}

/// <summary>
/// Central gün sonu (PerformEOD) command. Carries the STYS-side PosGunSonuIslemiId for result
/// routing; that field is a STYS domain concept and must never appear in the PAVO wire request.
/// </summary>
public sealed class PavoPerformEodCommand : PavoPerformEodRequest, IAgentCommand
{
    public int PosGunSonuIslemiId { get; set; }

    public string CommandType => "PavoPerformEOD";
}
