namespace STYS.Rezervasyonlar.Dto;

public class RezervasyonOdemeDto
{
    public int Id { get; set; }

    public DateTime OdemeTarihi { get; set; }

    public decimal OdemeTutari { get; set; }

    public string ParaBirimi { get; set; } = "TRY";

    public string OdemeTipi { get; set; } = string.Empty;

    public string? Aciklama { get; set; }
}

