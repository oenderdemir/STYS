using STYS.Muhasebe.Kdv.Enums;

namespace STYS.Muhasebe.Kdv.Dtos;

public class KdvHareketRaporFilterDto
{
    public DateTime BaslangicTarihi { get; set; }
    public DateTime BitisTarihi { get; set; }
    public int? TesisId { get; set; }
    public int? DepoId { get; set; }
    public KdvUygulamaTipi? KdvUygulamaTipi { get; set; }

    /// <summary>
    /// Muhasebe fiş durum filtresi:
    /// "Hepsi" = tüm stok hareketleri,
    /// "FisiOlan" = muhasebe fişi oluşmuş olanlar,
    /// "FisiOlmayan" = muhasebe fişi henüz oluşmamış olanlar.
    /// </summary>
    public string? MusFisDurumu { get; set; }
}

public class KdvHareketRaporDto
{
    public List<KdvHareketRaporSatirDto> Satirlar { get; set; } = [];
    public KdvHareketRaporOzetDto Ozet { get; set; } = new();
    public int ToplamKayitSayisi { get; set; }
}

public class KdvHareketRaporSatirDto
{
    public int Id { get; set; }
    public DateTime HareketTarihi { get; set; }
    public string HareketTipi { get; set; } = string.Empty;
    public string DepoAdi { get; set; } = string.Empty;
    public string TasinirKod { get; set; } = string.Empty;
    public string TasinirAd { get; set; } = string.Empty;
    public decimal Miktar { get; set; }
    public decimal BirimFiyat { get; set; }
    public decimal Tutar { get; set; }

    public int KdvUygulamaTipi { get; set; }
    public string KdvUygulamaTipiAd { get; set; } = string.Empty;
    public string? KdvIstisnaKodu { get; set; }
    public string? KdvIstisnaAciklamasi { get; set; }
    public decimal KdvOrani { get; set; }
    public decimal KdvTutari { get; set; }
    public decimal KdvliTutar { get; set; }

    public int? MusFisId { get; set; }
    public string? MusFisNo { get; set; }
    public string? MusFisDurumu { get; set; }

    public string? BelgeNo { get; set; }
    public string? Aciklama { get; set; }
}

public class KdvHareketRaporOzetDto
{
    public int ToplamKayitSayisi { get; set; }
    public int KdvliSayisi { get; set; }
    public int IstisnaliSayisi { get; set; }
    public int KdvKapsamDisiSayisi { get; set; }
    public int TevkifatliSayisi { get; set; }
    public int FisiOlanSayisi { get; set; }
    public int FisiOlmayanSayisi { get; set; }
    public decimal ToplamKdvTutari { get; set; }
    public decimal ToplamTutar { get; set; }
}
