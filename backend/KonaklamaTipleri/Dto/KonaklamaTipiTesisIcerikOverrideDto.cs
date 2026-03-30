namespace STYS.KonaklamaTipleri.Dto;

public class KonaklamaTipiTesisIcerikOverrideDto
{
    public int KonaklamaTipiIcerikKalemiId { get; set; }

    public string HizmetKodu { get; set; } = string.Empty;

    public string HizmetAdi { get; set; } = string.Empty;

    public bool OverrideVarMi { get; set; }

    public bool DevreDisiMi { get; set; }

    public int GlobalMiktar { get; set; } = 1;

    public string GlobalPeriyot { get; set; } = string.Empty;

    public string GlobalPeriyotAdi { get; set; } = string.Empty;

    public string GlobalKullanimTipi { get; set; } = string.Empty;

    public string GlobalKullanimTipiAdi { get; set; } = string.Empty;

    public string GlobalKullanimNoktasi { get; set; } = string.Empty;

    public string GlobalKullanimNoktasiAdi { get; set; } = string.Empty;

    public string? GlobalKullanimBaslangicSaati { get; set; }

    public string? GlobalKullanimBitisSaati { get; set; }

    public bool GlobalCheckInGunuGecerliMi { get; set; } = true;

    public bool GlobalCheckOutGunuGecerliMi { get; set; } = true;

    public string? GlobalAciklama { get; set; }

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
