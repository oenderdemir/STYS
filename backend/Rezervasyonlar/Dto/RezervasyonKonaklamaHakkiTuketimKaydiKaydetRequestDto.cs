namespace STYS.Rezervasyonlar.Dto;

public class RezervasyonKonaklamaHakkiTuketimKaydiKaydetRequestDto
{
    public int? IsletmeAlaniId { get; set; }

    public DateTime TuketimTarihi { get; set; }

    public int Miktar { get; set; } = 1;

    public string? Aciklama { get; set; }
}
