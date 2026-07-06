namespace STYS.Raporlar.GecikenCheckIn.Dto;

public class GecikenCheckInRaporDto
{
    public int TesisId { get; set; }
    public string? TesisAdi { get; set; }
    public DateTime ReferansTarihi { get; set; }
    public int? OdaTipiId { get; set; }
    public string? OdaTipiAdi { get; set; }
    public string GecikmeDurumu { get; set; } = "tumu";
    public string Baslik { get; set; } = "";

    public GecikenCheckInOzetDto Ozet { get; set; } = new();
    public List<GecikenCheckInRezervasyonDto> Rezervasyonlar { get; set; } = [];
}

public class GecikenCheckInOzetDto
{
    public int ToplamRezervasyonSayisi { get; set; }
    public int BugunGirisSayisi { get; set; }
    public int GecikenSayisi { get; set; }
    public int KritikGecikenSayisi { get; set; }
    public int ToplamKisiSayisi { get; set; }
    public decimal ToplamKalanTutar { get; set; }
}

public class GecikenCheckInRezervasyonDto
{
    public int RezervasyonId { get; set; }
    public string? ReferansNo { get; set; }
    public string? MisafirAdiSoyadi { get; set; }
    public string? MisafirTelefon { get; set; }

    public DateTime GirisTarihi { get; set; }
    public DateTime CikisTarihi { get; set; }

    public int GecikenGunSayisi { get; set; }
    public int KisiSayisi { get; set; }

    public string RezervasyonDurumu { get; set; } = "";
    public string RezervasyonDurumuLabel { get; set; } = "";

    public string GecikmeDurumu { get; set; } = "";
    public string GecikmeDurumuLabel { get; set; } = "";

    public List<string> OdaNolari { get; set; } = [];
    public List<string> OdaTipleri { get; set; } = [];

    public decimal ToplamUcret { get; set; }
    public decimal OdenenTutar { get; set; }
    public decimal KalanTutar { get; set; }
    public string? ParaBirimi { get; set; }
}
