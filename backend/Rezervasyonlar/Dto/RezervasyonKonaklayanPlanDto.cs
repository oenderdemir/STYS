using STYS.Rezervasyonlar;

namespace STYS.Rezervasyonlar.Dto;

public class RezervasyonKonaklayanPlanDto
{
    public int RezervasyonId { get; set; }

    public int KisiSayisi { get; set; }

    public List<RezervasyonKonaklayanSegmentDto> Segmentler { get; set; } = [];

    public List<RezervasyonKonaklayanKisiDto> Konaklayanlar { get; set; } = [];
}

public class RezervasyonKonaklayanSegmentDto
{
    public int SegmentId { get; set; }

    public int SegmentSirasi { get; set; }

    public DateTime BaslangicTarihi { get; set; }

    public DateTime BitisTarihi { get; set; }

    public List<RezervasyonKonaklayanOdaSecenekDto> OdaSecenekleri { get; set; } = [];
}

public class RezervasyonKonaklayanOdaSecenekDto
{
    public int OdaId { get; set; }

    public string OdaNo { get; set; } = string.Empty;

    public string BinaAdi { get; set; } = string.Empty;

    public string OdaTipiAdi { get; set; } = string.Empty;

    public int AyrilanKisiSayisi { get; set; }

    public bool PaylasimliMi { get; set; }
}

public class RezervasyonKonaklayanKisiDto
{
    public int? Id { get; set; }
    public int SiraNo { get; set; }

    public string AdSoyad { get; set; } = string.Empty;

    public string? TcKimlikNo { get; set; }

    public string? PasaportNo { get; set; }

    public string? Cinsiyet { get; set; }

    public string? Ad { get; set; }
    public string? Soyad { get; set; }
    public string? KimlikTuru { get; set; }
    public string? KimlikNo { get; set; }
    public string? BelgeNo { get; set; }
    public string? BelgeTuru { get; set; }
    public string? UyrukKodu { get; set; }
    public DateTime? DogumTarihi { get; set; }
    public string? DogumYeri { get; set; }
    public string? Telefon { get; set; }
    public string? AracPlakasi { get; set; }
    public DateTime? FiiliGirisTarihi { get; set; }
    public DateTime? FiiliCikisTarihi { get; set; }
    public string? KonaklamaKullanimSekli { get; set; }
    public string KbsDurumu { get; set; } = "BildirimYok";
    public string? SonKbsBildirimSonucu { get; set; }
    public List<string> EksikKbsBilgileri { get; set; } = [];

    public string KatilimDurumu { get; set; } = KonaklayanKatilimDurumlari.Bekleniyor;

    public List<RezervasyonKonaklayanKisiAtamaDto> Atamalar { get; set; } = [];
}

public class RezervasyonKonaklayanKisiAtamaDto
{
    public int SegmentId { get; set; }

    public int? OdaId { get; set; }

    public int? YatakNo { get; set; }
}
