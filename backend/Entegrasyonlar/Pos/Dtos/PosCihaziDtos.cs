namespace STYS.Entegrasyonlar.Pos.Dtos;

public sealed class PosCihaziDto
{
    public int Id { get; set; }
    public int KurumId { get; set; }
    public int TesisId { get; set; }
    public string? TesisAd { get; set; }
    public int? AgentId { get; set; }
    public string? AgentAd { get; set; }
    public int Saglayici { get; set; }
    public string SaglayiciAd => Saglayici == 0 ? "PAVO" : "Diğer";
    public string Ad { get; set; } = string.Empty;
    public string SeriNo { get; set; } = string.Empty;
    public string? IpAdresi { get; set; }
    public int? HttpPort { get; set; }
    public int? HttpsPort { get; set; }
    public string? Fingerprint { get; set; }
    public bool EslesmeOnayliMi { get; set; }
    public bool AktifMi { get; set; }
    public DateTime? SonBaglantiTarihi { get; set; }
    public string? Aciklama { get; set; }
    public int TerminalSayisi { get; set; }
}

public sealed class PosCihaziKaydetRequest
{
    public int TesisId { get; set; }
    public int? AgentId { get; set; }
    public int Saglayici { get; set; }
    public string Ad { get; set; } = string.Empty;
    public string SeriNo { get; set; } = string.Empty;
    public string? IpAdresi { get; set; }
    public int? HttpPort { get; set; }
    public int? HttpsPort { get; set; }
    public string? Fingerprint { get; set; }
    public string? Aciklama { get; set; }
}
