namespace STYS.Muhasebe.Kdv.Dtos;

public class KdvOzetRaporFilterDto
{
    /// <summary>Mali yıl (örn. 2026). Donem ile birlikte kullanıldığında BaslangicTarihi/BitisTarihi otomatik hesaplanır.</summary>
    public int? MaliYil { get; set; }

    /// <summary>Dönem (1-12). MaliYil ile birlikte kullanıldığında ayın ilk ve son günü hesaplanır.</summary>
    public int? Donem { get; set; }

    /// <summary>Doğrudan başlangıç tarihi. MaliYil/Donem verilmişse override edilir.</summary>
    public DateTime? BaslangicTarihi { get; set; }

    /// <summary>Doğrudan bitiş tarihi. MaliYil/Donem verilmişse override edilir.</summary>
    public DateTime? BitisTarihi { get; set; }

    public int? TesisId { get; set; }
    public int? DepoId { get; set; }
    public int? TasinirKartId { get; set; }
    public string? HareketTipi { get; set; }
    public int? KdvUygulamaTipi { get; set; }
    public int? KdvIstisnaTanimId { get; set; }
    public string? KdvIstisnaKodu { get; set; }

    /// <summary>
    /// Muhasebe fiş durum filtresi:
    /// "Hepsi" = tüm stok hareketleri,
    /// "FisiOlan" = muhasebe fişi oluşmuş olanlar,
    /// "FisiOlmayan" = muhasebe fişi henüz oluşmamış olanlar.
    /// </summary>
    public string? MusFisDurumu { get; set; }
}

public class KdvOzetRaporDto
{
    public DateTime BaslangicTarihi { get; set; }
    public DateTime BitisTarihi { get; set; }
    public KdvOzetRaporOzetDto Ozet { get; set; } = new();
    public List<KdvUygulamaTipiOzetDto> UygulamaTipiOzetleri { get; set; } = [];
    public List<KdvIstisnaKoduOzetDto> IstisnaKoduOzetleri { get; set; } = [];
    public List<KdvOzetRaporUyariDto> Uyarilar { get; set; } = [];
}

public class KdvOzetRaporOzetDto
{
    /// <summary>Dönem etiketi (örn. "2026 Mayıs" veya "01.01.2026 — 31.05.2026").</summary>
    public string DonemLabel { get; set; } = string.Empty;

    // Satış (Hesaplanan KDV)
    public int SatisHareketSayisi { get; set; }
    public decimal SatisMatrahi { get; set; }
    public decimal HesaplananKdvTutari { get; set; }

    // Alış (İndirilecek KDV)
    public int AlisHareketSayisi { get; set; }
    public decimal AlisMatrahi { get; set; }
    public decimal IndirilecekKdvTutari { get; set; }

    // Net KDV
    public decimal NetKdv { get; set; }

    // İstisna / Kapsam Dışı
    public decimal IstisnaMatrahi { get; set; }
    public decimal KapsamDisiMatrah { get; set; }

    // Genel
    public int ToplamKayitSayisi { get; set; }
    public int FisiOlanSayisi { get; set; }
    public int FisiOlmayanSayisi { get; set; }
}

public class KdvUygulamaTipiOzetDto
{
    public int KdvUygulamaTipi { get; set; }
    public string KdvUygulamaTipiAd { get; set; } = string.Empty;
    public int HareketSayisi { get; set; }
    public decimal Matrah { get; set; }
    public decimal KdvTutari { get; set; }
}

public class KdvIstisnaKoduOzetDto
{
    public string? KdvIstisnaKodu { get; set; }
    public string? KdvIstisnaAciklamasi { get; set; }
    public int HareketSayisi { get; set; }
    public decimal Matrah { get; set; }
}

public class KdvOzetRaporUyariDto
{
    public string UyariKodu { get; set; } = string.Empty;
    public string UyariMesaji { get; set; } = string.Empty;
    public int EtkilenenKayitSayisi { get; set; }
}
