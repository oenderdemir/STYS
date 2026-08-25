using System.ComponentModel.DataAnnotations;
using STYS.Muhasebe.StokHareketleri.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.KantinYonetimi.KantinSatislari.Entities;

public static class KantinSatisIadeDurumlari
{
    public const string Taslak = "Taslak";
    public const string Kesinlesti = "Kesinlesti";
}

public static class KantinSatisIadeFinansalDurumlari
{
    public const string Bekliyor = "Bekliyor";
}

public class KantinSatisIade : BaseEntity<int>
{
    public int TesisId { get; set; }
    public int KantinSatisId { get; set; }
    public DateTime IadeTarihi { get; set; }

    [Required]
    [MaxLength(16)]
    public string Durum { get; set; } = KantinSatisIadeDurumlari.Taslak;

    [MaxLength(1024)]
    public string? Aciklama { get; set; }

    [MaxLength(64)]
    public string? OlusturanKullaniciId { get; set; }

    public DateTime? KesinlesmeTarihi { get; set; }

    [Required]
    [MaxLength(16)]
    public string FinansalIadeDurumu { get; set; } = KantinSatisIadeFinansalDurumlari.Bekliyor;

    public KantinSatis? KantinSatis { get; set; }
    public ICollection<KantinSatisIadeSatir> Satirlar { get; set; } = [];
}

public class KantinSatisIadeSatir : BaseEntity<int>
{
    public int KantinSatisIadeId { get; set; }
    public int KantinSatisSatirId { get; set; }
    public decimal Miktar { get; set; }

    // Orijinal satis satirindan snapshot'lar.
    public int TasinirKartId { get; set; }

    [Required]
    [MaxLength(64)]
    public string StokKodu { get; set; } = string.Empty;

    [Required]
    [MaxLength(256)]
    public string UrunAdi { get; set; } = string.Empty;

    [Required]
    [MaxLength(32)]
    public string Birim { get; set; } = string.Empty;

    [MaxLength(16)]
    public string TakipTipi { get; set; } = string.Empty;

    [MaxLength(128)]
    public string? LotNo { get; set; }

    [MaxLength(128)]
    public string? SeriNo { get; set; }

    public decimal BirimSatisFiyati { get; set; }
    public decimal KdvOrani { get; set; }

    // Orijinal satis StokHareket'inden maliyet snapshot'i.
    public decimal? MaliyetBirimFiyat { get; set; }
    public decimal? MaliyetTutari { get; set; }

    public int? StokHareketId { get; set; }

    public KantinSatisIade? KantinSatisIade { get; set; }
    public KantinSatisSatir? KantinSatisSatir { get; set; }
    public StokHareket? StokHareket { get; set; }
}
