namespace STYS.Muhasebe.StokLotlari.Dtos;

public class StokLotBakiyeDto
{
    public int StokLotId { get; set; }
    public string LotNo { get; set; } = string.Empty;
    public DateTime? SonKullanmaTarihi { get; set; }
    public decimal GirisMiktari { get; set; }
    public decimal CikisMiktari { get; set; }
    public decimal BakiyeMiktari { get; set; }
}

public static class StokLotSktUyariDurumlari
{
    public const string Gecmis = "Gecmis";
    public const string Kritik = "Kritik";
    public const string Yaklasiyor = "Yaklasiyor";
    public const string Normal = "Normal";
}

public static class StokLotSktUyariEsikleri
{
    public const int KritikGun = 7;
    public const int YaklasiyorGun = 30;
}

public class StokLotSktUyariDto
{
    public int DepoId { get; set; }
    public string DepoKod { get; set; } = string.Empty;
    public string DepoAd { get; set; } = string.Empty;
    public int TasinirKartId { get; set; }
    public string StokKodu { get; set; } = string.Empty;
    public string TasinirKartAd { get; set; } = string.Empty;
    public int StokLotId { get; set; }
    public string LotNo { get; set; } = string.Empty;
    public DateTime SonKullanmaTarihi { get; set; }
    public decimal KalanMiktar { get; set; }
    public int KalanGun { get; set; }
    public string Durum { get; set; } = StokLotSktUyariDurumlari.Normal;
}
