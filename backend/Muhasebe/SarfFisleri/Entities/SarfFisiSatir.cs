using System.ComponentModel.DataAnnotations;
using STYS.Muhasebe.StokHareketleri.Entities;
using STYS.Muhasebe.StokLotlari.Entities;
using STYS.Muhasebe.StokSerileri.Entities;
using STYS.Muhasebe.TasinirKartlari.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Muhasebe.SarfFisleri.Entities;

public class SarfFisiSatir : BaseEntity<int>
{
    public int SarfFisiId { get; set; }
    public int TasinirKartId { get; set; }
    public int? StokLotId { get; set; }
    public int? StokSeriId { get; set; }
    public int? StokHareketId { get; set; }

    [Required]
    [MaxLength(16)]
    public string TakipTipi { get; set; } = TasinirKartTakipTipleri.Yok;

    [Required]
    [MaxLength(64)]
    public string StokKodu { get; set; } = string.Empty;

    [Required]
    [MaxLength(256)]
    public string TasinirKartAd { get; set; } = string.Empty;

    [Required]
    [MaxLength(32)]
    public string Birim { get; set; } = "Adet";

    [MaxLength(64)]
    public string? LotNo { get; set; }

    public DateTime? SonKullanmaTarihi { get; set; }

    [MaxLength(128)]
    public string? SeriNo { get; set; }

    public decimal Miktar { get; set; }

    [MaxLength(1024)]
    public string? Aciklama { get; set; }

    public SarfFisi? SarfFisi { get; set; }
    public TasinirKart? TasinirKart { get; set; }
    public StokLot? StokLot { get; set; }
    public StokSeri? StokSeri { get; set; }
    public StokHareket? StokHareket { get; set; }
}
