using System.ComponentModel.DataAnnotations;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Kamp.Entities;

public class KampBasvuruSahibi : BaseEntity<int>
{
    [MaxLength(32)]
    public string? TcKimlikNo { get; set; }

    [Required]
    [MaxLength(200)]
    public string AdSoyad { get; set; } = string.Empty;

    [Required]
    [MaxLength(32)]
    public string BasvuruSahibiTipi { get; set; } = string.Empty;

    public int HizmetYili { get; set; }

    public Guid? UserId { get; set; }

    public bool AktifMi { get; set; } = true;

    public ICollection<KampBasvuru> Basvurular { get; set; } = [];

    public ICollection<KampBasvuruGecmisKatilim> GecmisKatilimlar { get; set; } = [];
}
