using System.ComponentModel.DataAnnotations;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.KonaklamaTipleri.Entities;

public class KonaklamaTipiIcerikKalemi : BaseEntity<int>
{
    public int KonaklamaTipiId { get; set; }

    [Required]
    [MaxLength(64)]
    public string HizmetKodu { get; set; } = string.Empty;

    public int Miktar { get; set; } = 1;

    [Required]
    [MaxLength(32)]
    public string Periyot { get; set; } = string.Empty;

    [Required]
    [MaxLength(32)]
    public string KullanimTipi { get; set; } = KonaklamaTipiIcerikKullanimTipleri.Adetli;

    [Required]
    [MaxLength(32)]
    public string KullanimNoktasi { get; set; } = KonaklamaTipiIcerikKullanimNoktalari.Genel;

    public TimeSpan? KullanimBaslangicSaati { get; set; }

    public TimeSpan? KullanimBitisSaati { get; set; }

    public bool CheckInGunuGecerliMi { get; set; } = true;

    public bool CheckOutGunuGecerliMi { get; set; } = true;

    [MaxLength(256)]
    public string? Aciklama { get; set; }

    public KonaklamaTipi? KonaklamaTipi { get; set; }

    public ICollection<TesisKonaklamaTipiIcerikOverride> TesisOverrideKalemleri { get; set; } = [];
}
