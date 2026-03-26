namespace STYS.Rezervasyonlar.Dto;

public class RezervasyonEkHizmetSecenekleriDto
{
    public int RezervasyonId { get; set; }

    public string ReferansNo { get; set; } = string.Empty;

    public List<RezervasyonEkHizmetMisafirSecenekDto> Misafirler { get; set; } = [];

    public List<RezervasyonEkHizmetTarifeSecenekDto> Tarifeler { get; set; } = [];
}

public class RezervasyonEkHizmetMisafirSecenekDto
{
    public int RezervasyonKonaklayanId { get; set; }

    public int SiraNo { get; set; }

    public string AdSoyad { get; set; } = string.Empty;
}

public class RezervasyonEkHizmetTarifeSecenekDto
{
    public int Id { get; set; }

    public int EkHizmetId { get; set; }

    public string Ad { get; set; } = string.Empty;

    public string? Aciklama { get; set; }

    public string BirimAdi { get; set; } = string.Empty;

    public decimal BirimFiyat { get; set; }

    public string ParaBirimi { get; set; } = "TRY";

    public DateTime BaslangicTarihi { get; set; }

    public DateTime BitisTarihi { get; set; }
}
