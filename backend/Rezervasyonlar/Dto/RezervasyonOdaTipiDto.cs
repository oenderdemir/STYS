namespace STYS.Rezervasyonlar.Dto;

public class RezervasyonOdaTipiDto
{
    public int Id { get; set; }

    public int TesisId { get; set; }

    public string Ad { get; set; } = string.Empty;

    public int Kapasite { get; set; }

    public bool PaylasimliMi { get; set; }
}
