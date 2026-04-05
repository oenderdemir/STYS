namespace STYS.Kamp.Dto;

public class KampTahsisListeDto
{
    public int Id { get; set; }

    public int Siralama { get; set; }

    public int KampDonemiId { get; set; }

    public string KampDonemiAd { get; set; } = string.Empty;

    public int TesisId { get; set; }

    public string TesisAd { get; set; } = string.Empty;

    public string BasvuruSahibiAdiSoyadi { get; set; } = string.Empty;

    public string BasvuruSahibiTipi { get; set; } = string.Empty;

    public string KonaklamaBirimiTipi { get; set; } = string.Empty;

    public string Durum { get; set; } = string.Empty;

    public int KatilimciSayisi { get; set; }

    public int OncelikSirasi { get; set; }

    public int Puan { get; set; }

    public decimal DonemToplamTutar { get; set; }

    public decimal AvansToplamTutar { get; set; }

    public int ToplamKontenjan { get; set; }

    public int TahsisEdilenSayisi { get; set; }

    public int KalanKontenjan { get; set; }

    public DateTime CreatedAt { get; set; }

    public List<string> Uyarilar { get; set; } = [];
}
