namespace STYS.Rezervasyonlar.Dto;

public class OdaRezervasyonTakvimiDto
{
    public int TesisId { get; set; }
    public string TesisAdi { get; set; } = string.Empty;
    public DateTime BaslangicTarihi { get; set; }
    public DateTime BitisTarihi { get; set; }
    public int GunSayisi { get; set; }

    public List<OdaRezervasyonTakvimGunDto> Gunler { get; set; } = [];
    public List<OdaRezervasyonOdaTipiGrupDto> OdaTipleri { get; set; } = [];
    public OdaRezervasyonTakvimOzetDto Ozet { get; set; } = new();
}

public class OdaRezervasyonTakvimGunDto
{
    public DateTime Tarih { get; set; }
    public string GunAdi { get; set; } = string.Empty;
    public string KisaGunAdi { get; set; } = string.Empty;
    public int Gun { get; set; }
    public int Ay { get; set; }
    public int Yil { get; set; }
    public bool BugunMu { get; set; }
    public int DoluOdaSayisi { get; set; }
    public int BosOdaSayisi { get; set; }
    public int CheckInSayisi { get; set; }
    public int CheckOutSayisi { get; set; }
    public int KisiSayisi { get; set; }
}

public class OdaRezervasyonOdaTipiGrupDto
{
    public int OdaTipiId { get; set; }
    public string OdaTipiAdi { get; set; } = string.Empty;
    public List<OdaRezervasyonOdaSatiriDto> Odalar { get; set; } = [];
}

public class OdaRezervasyonOdaSatiriDto
{
    public int OdaId { get; set; }
    public string OdaNo { get; set; } = string.Empty;
    public int OdaTipiId { get; set; }
    public string OdaTipiAdi { get; set; } = string.Empty;
    public int Kapasite { get; set; }
    public string? TemizlikDurumu { get; set; }
    public List<OdaRezervasyonBlokDto> Bloklar { get; set; } = [];
}

public class OdaRezervasyonBlokDto
{
    public string BlokTipi { get; set; } = string.Empty;

    public int? RezervasyonId { get; set; }
    public int? OdaKullanimBlokId { get; set; }
    public string Baslik { get; set; } = string.Empty;
    public string? AltBaslik { get; set; }

    public DateTime BaslangicTarihi { get; set; }
    public DateTime BitisTarihi { get; set; }

    public int BaslangicGunIndex { get; set; }
    public int GunUzunlugu { get; set; }
    public int GeceSayisi { get; set; }

    public bool SolKenaraDevamEdiyor { get; set; }
    public bool SagKenaraDevamEdiyor { get; set; }

    public string Durum { get; set; } = string.Empty;
    public string RenkTipi { get; set; } = string.Empty;

    public decimal? ToplamUcret { get; set; }
    public decimal? OdenenTutar { get; set; }
    public decimal? KalanTutar { get; set; }
    public string? ParaBirimi { get; set; }

    public bool CheckInBugunMu { get; set; }
    public bool CheckOutBugunMu { get; set; }
    public bool OdaDegisimiGerekli { get; set; }
    public bool OdemeEksikMi { get; set; }

    public List<string> Uyarilar { get; set; } = [];
}

public class OdaRezervasyonTakvimOzetDto
{
    public int ToplamOdaSayisi { get; set; }
    public int DoluOdaSayisi { get; set; }
    public int BosOdaSayisi { get; set; }
    public int BugunCheckInSayisi { get; set; }
    public int BugunCheckOutSayisi { get; set; }
    public int YarinCheckInSayisi { get; set; }
    public int BugunKisiSayisi { get; set; }
    public int YarinGelecekKisiSayisi { get; set; }
    public decimal DonemToplamGelir { get; set; }
    public string ParaBirimi { get; set; } = "TRY";
}
