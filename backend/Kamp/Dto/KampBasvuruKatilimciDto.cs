namespace STYS.Kamp.Dto;

public class KampBasvuruKatilimciDto
{
    public int? Id { get; set; }
    public string AdSoyad { get; set; } = string.Empty;
    public string? TcKimlikNo { get; set; }
    public DateTime DogumTarihi { get; set; }
    public bool BasvuruSahibiMi { get; set; }
    public string KatilimciTipi { get; set; } = KampKatilimciTipleri.Kamu;
    public string AkrabalikTipi { get; set; } = KampAkrabalikTipleri.Diger;
    public bool KimlikBilgileriDogrulandiMi { get; set; }
    public bool YemekTalepEdiyorMu { get; set; } = true;
}
