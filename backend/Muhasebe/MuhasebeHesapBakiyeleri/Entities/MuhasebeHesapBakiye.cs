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

    public DateTime SonGuncellemeTarihi { get; set; }
}
