namespace STYS.Kamp.Dto;

public class KampDonemiTesisAtamaDto
{
    public int TesisId { get; set; }

    public string TesisAd { get; set; } = string.Empty;

    public bool AtamaVarMi { get; set; }

    public bool DonemdeAktifMi { get; set; }

    public bool BasvuruyaAcikMi { get; set; }

    public int ToplamKontenjan { get; set; }

    public string? Aciklama { get; set; }
}
