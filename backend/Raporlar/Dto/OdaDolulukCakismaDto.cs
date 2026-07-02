namespace STYS.Raporlar.Dto;

public class OdaDolulukCakismaDto
{
    public int RezervasyonId { get; set; }

    public string ReferansNo { get; set; } = "";

    public string? MisafirAdiSoyadi { get; set; }

    public DateTime GirisTarihi { get; set; }

    public DateTime CikisTarihi { get; set; }

    public string RezervasyonDurumu { get; set; } = "";
}
