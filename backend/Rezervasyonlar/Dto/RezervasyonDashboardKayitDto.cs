namespace STYS.Rezervasyonlar.Dto;

public class RezervasyonDashboardKayitDto
{
    public int Id { get; set; }

    public string ReferansNo { get; set; } = string.Empty;

    public string MisafirAdiSoyadi { get; set; } = string.Empty;

    public int KisiSayisi { get; set; }

    public DateTime GirisTarihi { get; set; }

    public DateTime CikisTarihi { get; set; }

    public string RezervasyonDurumu { get; set; } = string.Empty;
}

