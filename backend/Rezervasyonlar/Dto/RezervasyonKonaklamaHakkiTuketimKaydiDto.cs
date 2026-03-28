namespace STYS.Rezervasyonlar.Dto;

public class RezervasyonKonaklamaHakkiTuketimKaydiDto
{
    public int Id { get; set; }

    public int? IsletmeAlaniId { get; set; }

    public DateTime TuketimTarihi { get; set; }

    public int Miktar { get; set; } = 1;

    public string KullanimTipi { get; set; } = string.Empty;

    public string KullanimNoktasi { get; set; } = string.Empty;

    public string KullanimNoktasiAdi { get; set; } = string.Empty;

    public string? TuketimNoktasiAdi { get; set; }

    public string? Aciklama { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public DateTime? CreatedAt { get; set; }
}
