namespace STYS.Raporlar.OdaMusaitlik.Dto;

public class OdaMusaitlikRaporDto
{
    public int TesisId { get; set; }
    public string? TesisAdi { get; set; }
    public DateTime Baslangic { get; set; }
    public DateTime Bitis { get; set; }
    public string Durum { get; set; } = "tumu";
    public int? OdaTipiId { get; set; }
    public string? OdaTipiAdi { get; set; }
    public int? Kapasite { get; set; }
    public string Baslik { get; set; } = "";

    public OdaMusaitlikOzetDto Ozet { get; set; } = new();
    public List<OdaMusaitlikOdaDto> Odalar { get; set; } = [];
}

public class OdaMusaitlikOzetDto
{
    public int ToplamOdaSayisi { get; set; }
    public int TamamenBosOdaSayisi { get; set; }
    public int TamamenDoluOdaSayisi { get; set; }
    public int KismenMusaitOdaSayisi { get; set; }

    public int ToplamGunSayisi { get; set; }
    public int ToplamOdaGunSayisi { get; set; }
    public int BosOdaGunSayisi { get; set; }
    public int DoluOdaGunSayisi { get; set; }
    public decimal MusaitlikOrani { get; set; }
}

public class OdaMusaitlikOdaDto
{
    public int OdaId { get; set; }
    public string OdaNo { get; set; } = "";
    public string? BinaAdi { get; set; }
    public string? OdaTipiAdi { get; set; }
    public int Kapasite { get; set; }

    public string MusaitlikDurumu { get; set; } = "";
    public string MusaitlikDurumuLabel { get; set; } = "";

    public int ToplamGunSayisi { get; set; }
    public int BosGunSayisi { get; set; }
    public int DoluGunSayisi { get; set; }
    public decimal MusaitlikOrani { get; set; }

    public List<OdaMusaitlikGunDto> Gunler { get; set; } = [];
}

public class OdaMusaitlikGunDto
{
    public DateTime Tarih { get; set; }
    public string GunAdi { get; set; } = "";
    public bool BosMu { get; set; }
    public bool DoluMu { get; set; }
    public int? RezervasyonId { get; set; }
    public string? ReferansNo { get; set; }
    public string? MisafirAdiSoyadi { get; set; }
    public string? RezervasyonDurumu { get; set; }
    public string? RezervasyonDurumuLabel { get; set; }
}
