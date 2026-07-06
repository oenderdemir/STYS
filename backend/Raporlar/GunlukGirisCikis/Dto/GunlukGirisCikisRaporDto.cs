namespace STYS.Raporlar.GunlukGirisCikis.Dto;

public class GunlukGirisCikisRaporDto
{
    public int TesisId { get; set; }
    public string? TesisAdi { get; set; }
    public DateTime Tarih { get; set; }
    public string ListeTipi { get; set; } = "tumu";
    public string Baslik { get; set; } = "";

    public GunlukGirisCikisOzetDto Ozet { get; set; } = new();
    public List<GunlukGirisCikisRezervasyonDto> Rezervasyonlar { get; set; } = [];
}

public class GunlukGirisCikisOzetDto
{
    public int GirisSayisi { get; set; }
    public int CikisSayisi { get; set; }
    public int DevamEdenSayisi { get; set; }
    public int GecikenCikisSayisi { get; set; }
    public int ToplamRezervasyonSayisi { get; set; }
    public int ToplamKisiSayisi { get; set; }
    public decimal ToplamKalanTutar { get; set; }
    public string ParaBirimi { get; set; } = "TRY";
}

public class GunlukGirisCikisRezervasyonDto
{
    public int RezervasyonId { get; set; }
    public string? ReferansNo { get; set; }
    public string? MisafirAdiSoyadi { get; set; }

    // TODO: Rezervasyon veya musteri tarafinda kurum/unite bilgisi netlestiginde doldurulacak.
    public string? KurumUnite { get; set; }

    public DateTime GirisTarihi { get; set; }
    public DateTime CikisTarihi { get; set; }

    public string? RezervasyonDurumu { get; set; }
    public string? RezervasyonDurumuLabel { get; set; }

    public List<string> OdaNolari { get; set; } = [];
    public int KisiSayisi { get; set; }

    public decimal ToplamUcret { get; set; }
    public decimal OdenenTutar { get; set; }
    public decimal KalanTutar { get; set; }
    public string ParaBirimi { get; set; } = "TRY";

    public bool GirisYapacakMi { get; set; }
    public bool CikisYapacakMi { get; set; }
    public bool DevamEdiyorMu { get; set; }
    public bool GecikenCikisMi { get; set; }

    public string ListeDurumu { get; set; } = "";
    public string ListeDurumuLabel { get; set; } = "";
    public string? Aciklama { get; set; }
}
