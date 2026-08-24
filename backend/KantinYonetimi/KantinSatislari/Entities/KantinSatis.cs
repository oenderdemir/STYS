using System.ComponentModel.DataAnnotations;
using STYS.KantinYonetimi.Kantinler.Entities;
using STYS.Muhasebe.KasaBankaHesaplari.Entities;
using STYS.Muhasebe.PosTahsilatValorleri.Entities;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Entities;
using STYS.Muhasebe.StokHareketleri.Entities;
using STYS.Muhasebe.StokLotlari.Entities;
using STYS.Muhasebe.StokSerileri.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.KantinYonetimi.KantinSatislari.Entities;

public static class KantinSatisDurumlari
{
    public const string Taslak = "Taslak";
    public const string Kesinlesti = "Kesinlesti";
}

public class KantinSatis : BaseEntity<int>
{
    public int TesisId { get; set; }
    public int KantinId { get; set; }
    public DateTime SatisTarihi { get; set; }

    [Required]
    [MaxLength(32)]
    public string Durum { get; set; } = KantinSatisDurumlari.Taslak;

    public decimal ToplamTutar { get; set; }
    public decimal MatrahToplami { get; set; }
    public decimal KdvToplami { get; set; }

    [MaxLength(1024)]
    public string? Aciklama { get; set; }

    public DateTime? KesinlesmeTarihi { get; set; }

    public Kantin? Kantin { get; set; }
    public ICollection<KantinSatisSatir> Satirlar { get; set; } = [];
    public ICollection<KantinSatisOdeme> Odemeler { get; set; } = [];
}

public class KantinSatisSatir : BaseEntity<int>
{
    public int KantinSatisId { get; set; }
    public int KantinUrunId { get; set; }
    public int TasinirKartId { get; set; }
    public decimal Miktar { get; set; }
    public decimal BirimSatisFiyati { get; set; }
    public decimal KdvOrani { get; set; }
    public decimal Matrah { get; set; }
    public decimal KdvTutari { get; set; }
    public decimal ToplamTutar { get; set; }
    public int? StokLotId { get; set; }
    public int? StokSeriId { get; set; }
    public int? StokHareketId { get; set; }

    [MaxLength(128)]
    public string? Barkod { get; set; }

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

    public DateTime? SonKullanmaTarihi { get; set; }

    [MaxLength(128)]
    public string? SeriNo { get; set; }

    public KantinSatis? KantinSatis { get; set; }
    public KantinUrun? KantinUrun { get; set; }
    public STYS.Muhasebe.TasinirKartlari.Entities.TasinirKart? TasinirKart { get; set; }
    public StokLot? StokLot { get; set; }
    public StokSeri? StokSeri { get; set; }
    public StokHareket? StokHareket { get; set; }
}

public class KantinSatisOdeme : BaseEntity<int>
{
    public int KantinSatisId { get; set; }

    [Required]
    [MaxLength(32)]
    public string OdemeYontemi { get; set; } = string.Empty;

    public int? KasaBankaHesapId { get; set; }
    public int? TahsilatOdemeBelgesiId { get; set; }
    public decimal Tutar { get; set; }

    [MaxLength(64)]
    public string? HesapKodSnapshot { get; set; }

    [MaxLength(200)]
    public string? HesapAdSnapshot { get; set; }

    public KantinSatis? KantinSatis { get; set; }
    public KasaBankaHesap? KasaBankaHesap { get; set; }
    public TahsilatOdemeBelgesi? TahsilatOdemeBelgesi { get; set; }
}
