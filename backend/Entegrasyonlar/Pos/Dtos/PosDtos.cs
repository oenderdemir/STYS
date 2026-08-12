using System.Text.Json.Serialization;

namespace STYS.Entegrasyonlar.Pos.Dtos;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PavoDeviceHealthStatus
{
    Unknown = 0,
    Healthy = 1,
    Unreachable = 2,
    Timeout = 3,
    TlsError = 4,
    ProtocolError = 5,
    Stale = 6
}

public sealed class PosSaglayiciDto
{
    public string Kod { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public bool EslesmeDestekliyorMu { get; set; }
}

public sealed class PosTerminalDto
{
    public int Id { get; set; }
    public int KurumId { get; set; }
    public int TesisId { get; set; }
    public string? TesisAd { get; set; }
    public int? PosCihaziId { get; set; }
    public string? PosCihaziAd { get; set; }
    public int? KasaBankaHesapId { get; set; }
    public string? KasaBankaHesapAd { get; set; }
    public string SaglayiciKodu { get; set; } = string.Empty;
    public string? AcquirerId { get; set; }
    public string? AcquirerName { get; set; }
    public string Ad { get; set; } = string.Empty;
    public string TerminalId { get; set; } = string.Empty;
    public string? MerchantId { get; set; }
    public string SerialNumber { get; set; } = string.Empty;
    public string? SourceFingerprint { get; set; }
    public string? SourceTerminalReference { get; set; }
    public bool EslesmeOnayliMi { get; set; }
    public bool AktifMi { get; set; }
    public long? PairingId { get; set; }
    public string? PairingCode { get; set; }
}

public enum PavoOperationalReadiness
{
    Ready = 0,
    AgentOffline = 1,
    DeviceOffline = 2,
    NotProvisioned = 3,
    ReProvisionRequired = 4,
    PairingInvalid = 5,
    NoActiveTerminal = 6,
    NoAccountMapping = 7,
    Disabled = 8,
    OwnershipConflict = 9
}

public sealed class PosTerminalOperationalReadinessDto
{
    public int Id { get; set; }
    public string TerminalId { get; set; } = string.Empty;
    public string? AcquirerId { get; set; }
    public string? AcquirerName { get; set; }
    public bool Active { get; set; }
    public int? KasaBankaHesapId { get; set; }
    public bool AccountMapped { get; set; }
    public bool PaymentReady { get; set; }
    public string? StatusMessage { get; set; }
}

public sealed class PosOperationalReadinessDto
{
    public int PosCihaziId { get; set; }
    public PavoOperationalReadiness Status { get; set; }
    public bool Ready => Status == PavoOperationalReadiness.Ready;
    public PavoDeviceHealthStatus DeviceHealthStatus { get; set; } = PavoDeviceHealthStatus.Unknown;
    public bool AgentOnline { get; set; }
    public bool DeviceOnline { get; set; }
    public bool Provisioned { get; set; }
    public bool InSync { get; set; }
    public bool PairingValid { get; set; }
    public bool HasActiveTerminal { get; set; }
    public bool HasAccountMapping { get; set; }
    public bool Disabled { get; set; }
    public bool OwnershipConflict { get; set; }
    public DateTime? AgentLastHeartbeatAt { get; set; }
    public DateTime? DeviceLastConnectionAt { get; set; }
    public DateTime? LastHealthCheckAt { get; set; }
    public DateTime? LastHealthSuccessAt { get; set; }
    public string? LastHealthStatus { get; set; }
    public string? LastError { get; set; }
    public int ActiveTerminalCount { get; set; }
    public int AccountMappedTerminalCount { get; set; }
    public IReadOnlyCollection<PosTerminalOperationalReadinessDto> Terminals { get; set; } = [];
    public IReadOnlyCollection<string> Reasons { get; set; } = [];
}

public sealed class PosTerminalKaydetRequest
{
    public int? PosCihaziId { get; set; }
    public int TesisId { get; set; }
    public int? KasaBankaHesapId { get; set; }
    public string SaglayiciKodu { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public string TerminalId { get; set; } = string.Empty;
    public string? MerchantId { get; set; }
    public string SerialNumber { get; set; } = string.Empty;
    public string? SourceFingerprint { get; set; }
    public string? SourceTerminalReference { get; set; }
    public bool AktifMi { get; set; } = true;
}

public sealed class PosOdemeBaslatRequest
{
    public int RezervasyonId { get; set; }
    public int PosTerminalId { get; set; }
    public decimal Tutar { get; set; }
    public int? CariKartId { get; set; }
    public string? Aciklama { get; set; }
}

public sealed class PosOdemeIslemiDto
{
    public int Id { get; set; }
    public int? PosCihaziId { get; set; }
    public int? RezervasyonId { get; set; }
    public int PosTerminalId { get; set; }
    public int KasaBankaHesapId { get; set; }
    public Guid? AgentCommandId { get; set; }
    public string SaglayiciKodu { get; set; } = string.Empty;
    public string? SaglayiciIslemId { get; set; }
    public string? SaglayiciDurumKodu { get; set; }
    public string IslemReferansi { get; set; } = string.Empty;
    public string? SaleReference { get; set; }
    public decimal Tutar { get; set; }
    public string ParaBirimi { get; set; } = "TRY";
    public string Durum { get; set; } = string.Empty;
    public string? PavoResultCode { get; set; }
    public string? PavoMessage { get; set; }
    public string? HataMesaji { get; set; }
    public string? AcquirerId { get; set; }
    public string? TerminalId { get; set; }
    public string? MerchantId { get; set; }
    public string? RetrievalReferenceNo { get; set; }
    public string? AcquirerReference { get; set; }
    public string? AuthorizationCode { get; set; }
    public DateTime? BaslatilmaTarihi { get; set; }
    public DateTime? TamamlanmaTarihi { get; set; }
    public DateTime? SonSorgulamaTarihi { get; set; }
    public int SorgulamaDenemeSayisi { get; set; }
    public int? RezervasyonOdemeId { get; set; }
    public bool TamamlandiMi { get; set; }
}

public sealed class PosPaymentBaslatRequest
{
    public int PosTerminalId { get; set; }
    public decimal Tutar { get; set; }
    public string? ParaBirimi { get; set; } = "TRY";
    public string? Aciklama { get; set; }
    public int? PosOdemeIslemiId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
}
