namespace STYS.Rezervasyonlar.Dto;

public class RezervasyonListeDto
{
    public int Id { get; set; }

    public string ReferansNo { get; set; } = string.Empty;

    public int TesisId { get; set; }

    public string MisafirAdiSoyadi { get; set; } = string.Empty;

    public string MisafirTelefon { get; set; } = string.Empty;

    public string? MisafirEposta { get; set; }

    public string? TcKimlikNo { get; set; }

    public string? PasaportNo { get; set; }

    public string? MisafirCinsiyeti { get; set; }

    public int KisiSayisi { get; set; }

    public DateTime GirisTarihi { get; set; }

    public DateTime CikisTarihi { get; set; }

    public decimal ToplamUcret { get; set; }

    public decimal OdenenTutar { get; set; }

    public decimal KalanTutar { get; set; }

    public string ParaBirimi { get; set; } = "TRY";

    public string RezervasyonDurumu { get; set; } = string.Empty;

    public bool KonaklayanPlaniTamamlandi { get; set; }

    public int GelenKonaklayanSayisi { get; set; }

    public int BekleyenKonaklayanSayisi { get; set; }

    public bool OdaDegisimiGerekli { get; set; }
}
