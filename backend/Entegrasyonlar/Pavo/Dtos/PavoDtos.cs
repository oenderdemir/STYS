namespace STYS.Entegrasyonlar.Pavo.Dtos;

public sealed class PavoTerminalDto
{
    public int Id { get; set; }
    public int TesisId { get; set; }
    public int KasaBankaHesapId { get; set; }
    public string Ad { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string SourceFingerprint { get; set; } = string.Empty;
    public string? SourceTerminalReference { get; set; }
    public bool EslesmeOnayliMi { get; set; }
    public bool AktifMi { get; set; }
    public long? PairingId { get; set; }
    public string? PairingCode { get; set; }
}

public sealed class PavoTerminalKaydetRequest
{
    public int TesisId { get; set; }
    public int KasaBankaHesapId { get; set; }
    public string Ad { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string SourceFingerprint { get; set; } = string.Empty;
    public string? SourceTerminalReference { get; set; }
    public bool AktifMi { get; set; } = true;
}

public sealed class PavoOdemeBaslatRequest
{
    public int RezervasyonId { get; set; }
    public int PavoTerminalId { get; set; }
    public decimal Tutar { get; set; }
    public int? CariKartId { get; set; }
    public string? Aciklama { get; set; }
}

public sealed class PavoOdemeIslemiDto
{
    public int Id { get; set; }
    public int RezervasyonId { get; set; }
    public int PavoTerminalId { get; set; }
    public int KasaBankaHesapId { get; set; }
    public long? PaymentLinkId { get; set; }
    public string PaymentLinkReference { get; set; } = string.Empty;
    public decimal Tutar { get; set; }
    public string ParaBirimi { get; set; } = "TRY";
    public string Durum { get; set; } = string.Empty;
    public string? HataMesaji { get; set; }
    public int? RezervasyonOdemeId { get; set; }
    public bool TamamlandiMi { get; set; }
}
