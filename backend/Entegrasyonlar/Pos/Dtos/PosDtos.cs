namespace STYS.Entegrasyonlar.Pos.Dtos;

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
