namespace STYS.Kamp.Dto;

public class KampBasvuruOnizlemeDto
{
    public bool BasvuruGecerliMi { get; set; }
    public int OncelikSirasi { get; set; }
    public int Puan { get; set; }
    public decimal GunlukToplamTutar { get; set; }
    public decimal DonemToplamTutar { get; set; }
    public decimal AvansToplamTutar { get; set; }
    public decimal KalanOdemeTutari { get; set; }
    public int KullanilanKontenjan { get; set; }
    public int ToplamKontenjan { get; set; }
    public int BosKontenjan { get; set; }
    public string? KontenjanMesaji { get; set; }
    public List<int> GecmisKatilimYillari { get; set; } = [];
    public List<string> Hatalar { get; set; } = [];
    public List<string> Uyarilar { get; set; } = [];
}
