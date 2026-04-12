using System.ComponentModel.DataAnnotations;
using STYS.Restoranlar.Entities;
using STYS.RestoranSiparisleri.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.RestoranMasalari.Entities;

public class RestoranMasa : BaseEntity<int>
{
    public int RestoranId { get; set; }

    [Required]
    [MaxLength(32)]
    public string MasaNo { get; set; } = string.Empty;

    public int Kapasite { get; set; } = 1;

    [Required]
    [MaxLength(32)]
    public string Durum { get; set; } = RestoranMasaDurumlari.Musait;

    public bool AktifMi { get; set; } = true;

    public Restoran? Restoran { get; set; }

    public ICollection<RestoranSiparis> Siparisler { get; set; } = [];
}
