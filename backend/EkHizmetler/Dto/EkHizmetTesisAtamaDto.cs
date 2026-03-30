namespace STYS.EkHizmetler.Dto;

public class EkHizmetTesisAtamaDto
{
    public int GlobalEkHizmetTanimiId { get; set; }

    public string Ad { get; set; } = string.Empty;

    public string? Aciklama { get; set; }

    public string BirimAdi { get; set; } = string.Empty;

    public string? PaketIcerikHizmetKodu { get; set; }

    public bool GlobalAktifMi { get; set; }

    public bool TesisteKullanilabilirMi { get; set; }

    public int TarifeSayisi { get; set; }
}
