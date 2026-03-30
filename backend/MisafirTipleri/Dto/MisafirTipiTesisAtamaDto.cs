namespace STYS.MisafirTipleri.Dto;

public class MisafirTipiTesisAtamaDto
{
    public int MisafirTipiId { get; set; }

    public string Kod { get; set; } = string.Empty;

    public string Ad { get; set; } = string.Empty;

    public bool GlobalAktifMi { get; set; }

    public bool TesisteKullanilabilirMi { get; set; }
}
