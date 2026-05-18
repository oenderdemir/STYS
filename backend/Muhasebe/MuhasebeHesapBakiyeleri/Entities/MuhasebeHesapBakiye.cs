using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;
using STYS.Tesisler.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Muhasebe.MuhasebeHesapBakiyeleri.Entities;

public class MuhasebeHesapBakiye : BaseEntity<int>
{
    public int TesisId { get; set; }
    public Tesis? Tesis { get; set; }

    public int MaliYil { get; set; }
    public int Donem { get; set; }

    public int MuhasebeHesapPlaniId { get; set; }
    public MuhasebeHesapPlani? MuhasebeHesapPlani { get; set; }

    public string HesapKodu { get; set; } = string.Empty;
    public string HesapAdi { get; set; } = string.Empty;

    /// <summary>
    /// false: Doğrudan hareket gören hesabın kendi bakiyesi.
    /// true: Alt hesap hareketlerinin üst hesaba yansıtılmış konsolide bakiyesi.
    /// </summary>
    public bool KonsolideMi { get; set; }

    public decimal BorcToplam { get; set; }
    public decimal AlacakToplam { get; set; }

    public decimal BorcBakiye { get; set; }
    public decimal AlacakBakiye { get; set; }

    /// <summary>
    /// BorcToplam - AlacakToplam. Pozitif = borç bakiyesi, negatif = alacak bakiyesi.
    /// </summary>
    public decimal NetBakiye { get; set; }

    /// <summary>
    /// Borc / Alacak / Sifir
    /// </summary>
    public string BakiyeTipi { get; set; } = string.Empty;

    /// <summary>
    /// HesapKodu segment sayısı. Örn: "150" → 1, "150.01" → 2, "150.01.001" → 3
    /// </summary>
    public int HesapSeviyesi { get; set; }

    /// <summary>
    /// Bir üst hesabın tam kodu. Örn: "150.01.001" → "150.01", "150.01" → "150", "150" → null
    /// </summary>
    public string? UstHesapKodu { get; set; }

    public DateTime SonGuncellemeTarihi { get; set; }
}
