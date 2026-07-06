namespace STYS.Raporlar.RezervasyonDurumDagilimi.Dto;

public class RezervasyonDurumDagilimiRaporDto
{
    public int TesisId { get; set; }
    public string? TesisAdi { get; set; }
    public DateTime Baslangic { get; set; }
    public DateTime Bitis { get; set; }

    public int? OdaTipiId { get; set; }
    public string? OdaTipiAdi { get; set; }
    public string? Durum { get; set; }
    public string? DurumLabel { get; set; }

    public string Baslik { get; set; } = "";

    public RezervasyonDurumDagilimiOzetDto Ozet { get; set; } = new();
    public List<RezervasyonDurumDagilimiDurumSatiriDto> Durumlar { get; set; } = [];
    public List<RezervasyonDurumDagilimiOdaTipiSatiriDto> OdaTipleri { get; set; } = [];
    public List<RezervasyonDurumDagilimiRezervasyonDto> Rezervasyonlar { get; set; } = [];
}

public class RezervasyonDurumDagilimiOzetDto
{
    public int ToplamRezervasyonSayisi { get; set; }

    public int TaslakSayisi { get; set; }
    public int OnayliSayisi { get; set; }
    public int CheckInTamamlandiSayisi { get; set; }
    public int CheckOutTamamlandiSayisi { get; set; }
    public int IptalSayisi { get; set; }

    public int GerceklesenRezervasyonSayisi { get; set; }
    public int DevamEdenRezervasyonSayisi { get; set; }

    public decimal IptalOrani { get; set; }
    public decimal GerceklesmeOrani { get; set; }
    public decimal CheckInOrani { get; set; }
    public decimal CheckOutOrani { get; set; }

    public int ToplamKisiSayisi { get; set; }
    public int ToplamGeceSayisi { get; set; }
}

public class RezervasyonDurumDagilimiDurumSatiriDto
{
    public string Durum { get; set; } = "";
    public string DurumLabel { get; set; } = "";
    public int RezervasyonSayisi { get; set; }
    public int KisiSayisi { get; set; }
    public int GeceSayisi { get; set; }
    public decimal Oran { get; set; }
}

public class RezervasyonDurumDagilimiOdaTipiSatiriDto
{
    public int OdaTipiId { get; set; }
    public string OdaTipiAdi { get; set; } = "";
    public int RezervasyonSayisi { get; set; }
    public int IptalSayisi { get; set; }
    public int GerceklesenSayisi { get; set; }
    public int KisiSayisi { get; set; }
    public int GeceSayisi { get; set; }
    public decimal IptalOrani { get; set; }
    public decimal GerceklesmeOrani { get; set; }
}

public class RezervasyonDurumDagilimiRezervasyonDto
{
    public int RezervasyonId { get; set; }
    public string? ReferansNo { get; set; }
    public string? MisafirAdiSoyadi { get; set; }

    public DateTime GirisTarihi { get; set; }
    public DateTime CikisTarihi { get; set; }

    public int GeceSayisi { get; set; }
    public int KisiSayisi { get; set; }

    public string RezervasyonDurumu { get; set; } = "";
    public string RezervasyonDurumuLabel { get; set; } = "";

    public List<string> OdaNolari { get; set; } = [];
    public List<string> OdaTipleri { get; set; } = [];
}
