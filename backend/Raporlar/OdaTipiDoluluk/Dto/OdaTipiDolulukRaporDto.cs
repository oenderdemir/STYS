namespace STYS.Raporlar.OdaTipiDoluluk.Dto;

public class OdaTipiDolulukRaporDto
{
    public int TesisId { get; set; }
    public string? TesisAdi { get; set; }
    public DateTime Baslangic { get; set; }
    public DateTime Bitis { get; set; }
    public int? OdaTipiId { get; set; }
    public string? OdaTipiAdi { get; set; }
    public string Baslik { get; set; } = "";

    public OdaTipiDolulukOzetDto Ozet { get; set; } = new();
    public List<OdaTipiDolulukSatirDto> OdaTipleri { get; set; } = [];
}

public class OdaTipiDolulukOzetDto
{
    public int ToplamOdaTipiSayisi { get; set; }
    public int ToplamOdaSayisi { get; set; }
    public int ToplamKapasite { get; set; }
    public int ToplamGunSayisi { get; set; }
    public int ToplamOdaGunSayisi { get; set; }
    public int DoluOdaGunSayisi { get; set; }
    public int BosOdaGunSayisi { get; set; }
    public decimal DolulukOrani { get; set; }
    public decimal MusaitlikOrani { get; set; }
}

public class OdaTipiDolulukSatirDto
{
    public int OdaTipiId { get; set; }
    public string OdaTipiAdi { get; set; } = "";
    public int OdaSayisi { get; set; }
    public int ToplamKapasite { get; set; }

    public int ToplamGunSayisi { get; set; }
    public int ToplamOdaGunSayisi { get; set; }
    public int DoluOdaGunSayisi { get; set; }
    public int BosOdaGunSayisi { get; set; }

    public decimal DolulukOrani { get; set; }
    public decimal MusaitlikOrani { get; set; }

    public int ToplamRezervasyonSayisi { get; set; }
    public int ToplamKonaklayanKisiSayisi { get; set; }
    public int ToplamKisiGeceSayisi { get; set; }

    public List<OdaTipiDolulukOdaDto> Odalar { get; set; } = [];
}

public class OdaTipiDolulukOdaDto
{
    public int OdaId { get; set; }
    public string OdaNo { get; set; } = "";
    public string? BinaAdi { get; set; }
    public int Kapasite { get; set; }

    public int ToplamGunSayisi { get; set; }
    public int DoluGunSayisi { get; set; }
    public int BosGunSayisi { get; set; }
    public decimal DolulukOrani { get; set; }
}
