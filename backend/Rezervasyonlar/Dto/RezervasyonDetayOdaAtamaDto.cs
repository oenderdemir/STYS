namespace STYS.Rezervasyonlar.Dto;

public class RezervasyonDetayOdaAtamaDto
{
    public int OdaId { get; set; }

    public string OdaNo { get; set; } = string.Empty;

    public string BinaAdi { get; set; } = string.Empty;

    public string OdaTipiAdi { get; set; } = string.Empty;

    public int AyrilanKisiSayisi { get; set; }

    public int Kapasite { get; set; }

    public bool PaylasimliMi { get; set; }
}
