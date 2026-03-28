namespace STYS.Rezervasyonlar.Dto;

public class RezervasyonKonaklamaTipiIcerikDto
{
    public string HizmetKodu { get; set; } = string.Empty;

    public string HizmetAdi { get; set; } = string.Empty;

    public int Miktar { get; set; } = 1;

    public string Periyot { get; set; } = string.Empty;

    public string PeriyotAdi { get; set; } = string.Empty;

    public string KullanimTipi { get; set; } = string.Empty;

    public string KullanimTipiAdi { get; set; } = string.Empty;

    public string KullanimNoktasi { get; set; } = string.Empty;

    public string KullanimNoktasiAdi { get; set; } = string.Empty;

    public string? KullanimBaslangicSaati { get; set; }

    public string? KullanimBitisSaati { get; set; }

    public bool CheckInGunuGecerliMi { get; set; } = true;

    public bool CheckOutGunuGecerliMi { get; set; } = true;

    public string? Aciklama { get; set; }
}
