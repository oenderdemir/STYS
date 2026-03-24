namespace STYS.Rezervasyonlar.Dto;

public class RezervasyonOdaDegisimAdayOdaDto
{
    public int OdaId { get; set; }

    public string OdaNo { get; set; } = string.Empty;

    public string BinaAdi { get; set; } = string.Empty;

    public string OdaTipiAdi { get; set; } = string.Empty;

    public bool PaylasimliMi { get; set; }

    public int Kapasite { get; set; }

    public int KalanKapasite { get; set; }

    public List<int> OnerilenYatakNolari { get; set; } = [];
}
