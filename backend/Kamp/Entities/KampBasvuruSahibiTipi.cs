using System.ComponentModel.DataAnnotations;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Kamp.Entities;

public class KampBasvuruSahibiTipi : BaseEntity<int>
{
    [Required]
    [MaxLength(64)]
    public string Kod { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string Ad { get; set; } = string.Empty;

    public int OncelikSirasi { get; set; }

    public int TabanPuan { get; set; }

    public bool HizmetYiliPuaniAktifMi { get; set; }

    public int EmekliBonusPuani { get; set; }

    [MaxLength(64)]
    public string? VarsayilanKatilimciTipiKodu { get; set; }

    public bool AktifMi { get; set; } = true;
}
