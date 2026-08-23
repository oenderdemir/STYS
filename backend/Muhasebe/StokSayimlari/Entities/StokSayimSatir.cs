using System.ComponentModel.DataAnnotations;
using STYS.Muhasebe.StokLotlari.Entities;
using STYS.Muhasebe.StokSerileri.Entities;
using STYS.Muhasebe.TasinirKartlari.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Muhasebe.StokSayimlari.Entities;

public class StokSayimSatir : BaseEntity<int>
{
    public int StokSayimId { get; set; }
    public int TasinirKartId { get; set; }
    public int? StokLotId { get; set; }
    public int? StokSeriId { get; set; }

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

    public decimal SistemMiktari { get; set; }
    public decimal SayilanMiktar { get; set; }
    public decimal FarkMiktari { get; set; }

    public StokSayim? StokSayim { get; set; }
    public TasinirKart? TasinirKart { get; set; }
    public StokLot? StokLot { get; set; }
    public StokSeri? StokSeri { get; set; }
}
