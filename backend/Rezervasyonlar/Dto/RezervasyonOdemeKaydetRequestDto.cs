namespace STYS.Rezervasyonlar.Dto;

public class RezervasyonOdemeKaydetRequestDto
{
    public decimal OdemeTutari { get; set; }

    public string OdemeTipi { get; set; } = OdemeTipleri.Nakit;

    public string? Aciklama { get; set; }
}

