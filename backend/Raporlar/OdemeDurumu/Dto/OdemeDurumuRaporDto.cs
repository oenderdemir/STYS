namespace STYS.Raporlar.OdemeDurumu.Dto;

public class OdemeDurumuRaporDto
{
    public int TesisId { get; set; }
    public string? TesisAdi { get; set; }
    public DateTime Baslangic { get; set; }
    public DateTime Bitis { get; set; }
    public string OdemeDurumu { get; set; } = "borclu";

    public OdemeDurumuOzetDto Ozet { get; set; } = new();
    public List<OdemeDurumuRezervasyonDto> Rezervasyonlar { get; set; } = [];
}

public class OdemeDurumuOzetDto
{
    public int ToplamRezervasyonSayisi { get; set; }
    public int BorcluRezervasyonSayisi { get; set; }
    public int OdemesiOlmayanRezervasyonSayisi { get; set; }
    public int KismiOdendiRezervasyonSayisi { get; set; }
    public int TamamenOdendiRezervasyonSayisi { get; set; }
    public int CikisYapmisBorcluRezervasyonSayisi { get; set; }
    public decimal ToplamUcret { get; set; }
    public decimal ToplamOdenenTutar { get; set; }
    public decimal ToplamKalanTutar { get; set; }
    public string ParaBirimi { get; set; } = "TRY";
}

public class OdemeDurumuRezervasyonDto
{
    public int RezervasyonId { get; set; }
    public string ReferansNo { get; set; } = string.Empty;
    public string MisafirAdiSoyadi { get; set; } = string.Empty;

    // TODO: Kurum/unite bilgisi mevcut rezervasyon veri modelinde tutulmuyor; ileride eklenirse doldurulacak.
    public string? KurumUnite { get; set; }

    public DateTime GirisTarihi { get; set; }
    public DateTime CikisTarihi { get; set; }
    public string RezervasyonDurumu { get; set; } = string.Empty;
    public string RezervasyonDurumuLabel { get; set; } = string.Empty;
    public List<string> OdaNolari { get; set; } = [];
    public int KisiSayisi { get; set; }

    public decimal ToplamUcret { get; set; }
    public decimal OdenenTutar { get; set; }
    public decimal KalanTutar { get; set; }
    public string ParaBirimi { get; set; } = "TRY";

    public string OdemeDurumu { get; set; } = string.Empty;
    public string OdemeDurumuLabel { get; set; } = string.Empty;
    public DateTime? SonOdemeTarihi { get; set; }
    public int OdemeSayisi { get; set; }

    public bool BorcluMu { get; set; }
    public bool CikisYapmisMi { get; set; }
    public bool CikisYapmisBorcluMu { get; set; }
}
