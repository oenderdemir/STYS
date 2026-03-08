namespace STYS.Rezervasyonlar.Dto;

public class RezervasyonDegisiklikGecmisiDto
{
    public int Id { get; set; }

    public string IslemTipi { get; set; } = string.Empty;

    public string? Aciklama { get; set; }

    public string? OncekiDegerJson { get; set; }

    public string? YeniDegerJson { get; set; }

    public DateTime CreatedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
}
