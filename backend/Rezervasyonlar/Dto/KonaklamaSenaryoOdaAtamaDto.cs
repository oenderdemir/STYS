namespace STYS.Rezervasyonlar.Dto;

public class KonaklamaSenaryoOdaAtamaDto
{
    public int OdaId { get; set; }

    public string OdaNo { get; set; } = string.Empty;

    public int BinaId { get; set; }

    public string BinaAdi { get; set; } = string.Empty;

    public int OdaTipiId { get; set; }

    public string OdaTipiAdi { get; set; } = string.Empty;

    public bool PaylasimliMi { get; set; }

    public int Kapasite { get; set; }

    public int AyrilanKisiSayisi { get; set; }
}
