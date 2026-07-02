namespace STYS.Raporlar.Dto;

public class OdaDolulukOzetDto
{
    public int ToplamOdaSayisi { get; set; }

    public int GunSayisi { get; set; }

    public int ToplamOdaGunSayisi { get; set; }

    public int DoluOdaGunSayisi { get; set; }

    public int BosOdaGunSayisi { get; set; }

    public decimal DolulukOraniYuzde { get; set; }

    public decimal ToplamTahsilat { get; set; }

    public decimal ToplamKalanTutar { get; set; }

    public decimal AyIcindeTahsilEdilenTutar { get; set; }

    public decimal KonaklayanRezervasyonlarinToplamTahsilati { get; set; }

    public decimal KonaklayanRezervasyonlarinToplamKalanTutari { get; set; }
}
