namespace STYS.Raporlar.OrtalamaKonaklamaSuresi.Dto;

public class OrtalamaKonaklamaSuresiRaporDto
{
    public int TesisId { get; set; }
    public string? TesisAdi { get; set; }
    public DateTime Baslangic { get; set; }
    public DateTime Bitis { get; set; }
    public int? OdaTipiId { get; set; }
    public string? OdaTipiAdi { get; set; }
    public string Baslik { get; set; } = "";

    public OrtalamaKonaklamaSuresiOzetDto Ozet { get; set; } = new();
    public List<OrtalamaKonaklamaSuresiOdaTipiDto> OdaTipleri { get; set; } = [];
    public List<OrtalamaKonaklamaSuresiRezervasyonDto> Rezervasyonlar { get; set; } = [];
}

public class OrtalamaKonaklamaSuresiOzetDto
{
    public int ToplamRezervasyonSayisi { get; set; }
    public int ToplamKisiSayisi { get; set; }
    public int ToplamGeceSayisi { get; set; }
    public decimal OrtalamaGeceSayisi { get; set; }
    public int EnKisaKonaklamaGece { get; set; }
    public int EnUzunKonaklamaGece { get; set; }

    public int KisaKonaklamaSayisi { get; set; }
    public int OrtaKonaklamaSayisi { get; set; }
    public int UzunKonaklamaSayisi { get; set; }
}

public class OrtalamaKonaklamaSuresiOdaTipiDto
{
    public int OdaTipiId { get; set; }
    public string OdaTipiAdi { get; set; } = "";
    public int RezervasyonSayisi { get; set; }
    public int ToplamKisiSayisi { get; set; }
    public int ToplamGeceSayisi { get; set; }
    public decimal OrtalamaGeceSayisi { get; set; }
    public int EnKisaKonaklamaGece { get; set; }
    public int EnUzunKonaklamaGece { get; set; }
}

public class OrtalamaKonaklamaSuresiRezervasyonDto
{
    public int RezervasyonId { get; set; }
    public string? ReferansNo { get; set; }
    public string? MisafirAdiSoyadi { get; set; }

    public DateTime GirisTarihi { get; set; }
    public DateTime CikisTarihi { get; set; }

    public int GeceSayisi { get; set; }
    public int KisiSayisi { get; set; }

    public List<string> OdaNolari { get; set; } = [];
    public List<string> OdaTipleri { get; set; } = [];

    public string? RezervasyonDurumu { get; set; }
    public string? RezervasyonDurumuLabel { get; set; }

    public string KonaklamaGrubu { get; set; } = "";
    public string KonaklamaGrubuLabel { get; set; } = "";
}
