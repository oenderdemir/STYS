using System.ComponentModel.DataAnnotations;
using STYS.KonaklamaTipleri;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Rezervasyonlar.Entities;

public class RezervasyonKonaklamaHakki : BaseEntity<int>
{
    public int RezervasyonId { get; set; }

    [Required]
    [MaxLength(64)]
    public string HizmetKodu { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string HizmetAdiSnapshot { get; set; } = string.Empty;

    public int Miktar { get; set; } = 1;

    [Required]
    [MaxLength(32)]
    public string Periyot { get; set; } = string.Empty;

    [MaxLength(64)]
    public string PeriyotAdiSnapshot { get; set; } = string.Empty;

    [Required]
    [MaxLength(32)]
    public string KullanimTipi { get; set; } = KonaklamaTipiIcerikKullanimTipleri.Adetli;

    [MaxLength(64)]
    public string KullanimTipiAdiSnapshot { get; set; } = string.Empty;

    [Required]
    [MaxLength(32)]
    public string KullanimNoktasi { get; set; } = KonaklamaTipiIcerikKullanimNoktalari.Genel;

    [MaxLength(64)]
    public string KullanimNoktasiAdiSnapshot { get; set; } = string.Empty;

    public TimeSpan? KullanimBaslangicSaati { get; set; }

    public TimeSpan? KullanimBitisSaati { get; set; }

    public bool CheckInGunuGecerliMi { get; set; } = true;

    public bool CheckOutGunuGecerliMi { get; set; } = true;

    public DateTime? HakTarihi { get; set; }

    [MaxLength(256)]
    public string? AciklamaSnapshot { get; set; }

    [Required]
    [MaxLength(32)]
    public string Durum { get; set; } = RezervasyonKonaklamaHakDurumlari.Bekliyor;

    public bool AktifMi { get; set; } = true;

    public Rezervasyon? Rezervasyon { get; set; }

    public ICollection<RezervasyonKonaklamaHakkiTuketimKaydi> TuketimKayitlari { get; set; } = [];
}
