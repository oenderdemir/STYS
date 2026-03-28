using System.ComponentModel.DataAnnotations;
using STYS.IsletmeAlanlari.Entities;
using STYS.KonaklamaTipleri;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Rezervasyonlar.Entities;

public class RezervasyonKonaklamaHakkiTuketimKaydi : BaseEntity<int>
{
    public int RezervasyonId { get; set; }

    public int RezervasyonKonaklamaHakkiId { get; set; }

    public int? IsletmeAlaniId { get; set; }

    public DateTime TuketimTarihi { get; set; }

    public int Miktar { get; set; } = 1;

    [Required]
    [MaxLength(32)]
    public string KullanimTipi { get; set; } = KonaklamaTipiIcerikKullanimTipleri.Adetli;

    [Required]
    [MaxLength(32)]
    public string KullanimNoktasi { get; set; } = KonaklamaTipiIcerikKullanimNoktalari.Genel;

    [Required]
    [MaxLength(64)]
    public string KullanimNoktasiAdiSnapshot { get; set; } = string.Empty;

    [MaxLength(128)]
    public string? TuketimNoktasiAdi { get; set; }

    [MaxLength(256)]
    public string? Aciklama { get; set; }

    public bool AktifMi { get; set; } = true;

    public Rezervasyon? Rezervasyon { get; set; }

    public RezervasyonKonaklamaHakki? RezervasyonKonaklamaHakki { get; set; }

    public IsletmeAlani? IsletmeAlani { get; set; }
}
