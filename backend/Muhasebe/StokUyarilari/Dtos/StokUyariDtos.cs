namespace STYS.Muhasebe.StokUyarilari.Dtos;

public static class StokUyariDurumlari
{
    public const string Kritik = "Kritik";
    public const string Dusuk = "Dusuk";
    public const string Normal = "Normal";
}

public class StokUyariDto
{
    public int DepoId { get; set; }
    public string DepoKod { get; set; } = string.Empty;
    public string DepoAd { get; set; } = string.Empty;
    public int TasinirKartId { get; set; }
    public string StokKodu { get; set; } = string.Empty;
    public string TasinirKartAd { get; set; } = string.Empty;
    public decimal MevcutMiktar { get; set; }
    public decimal? MinimumStokMiktari { get; set; }
    public decimal? KritikStokMiktari { get; set; }
    public string Durum { get; set; } = StokUyariDurumlari.Normal;
}

