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
    public int TesisId { get; set; }
    public int KasaBankaHesapId { get; set; }
    public string SaglayiciKodu { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
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
    public int TesisId { get; set; }
    public int KasaBankaHesapId { get; set; }
    public string SaglayiciKodu { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
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
    public int RezervasyonId { get; set; }
    public int PosTerminalId { get; set; }
    public int KasaBankaHesapId { get; set; }
    public string SaglayiciKodu { get; set; } = string.Empty;
    public string? SaglayiciIslemId { get; set; }
    public string? SaglayiciDurumKodu { get; set; }
    public string IslemReferansi { get; set; } = string.Empty;
    public decimal Tutar { get; set; }
    public string ParaBirimi { get; set; } = "TRY";
    public string Durum { get; set; } = string.Empty;
    public string? HataMesaji { get; set; }
    public DateTime? SonSorgulamaTarihi { get; set; }
    public int SorgulamaDenemeSayisi { get; set; }
    public int? RezervasyonOdemeId { get; set; }
    public bool TamamlandiMi { get; set; }
}
