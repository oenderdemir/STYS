using System.ComponentModel.DataAnnotations;
using STYS.RestoranMenuKategorileri.Entities;
using STYS.RestoranSiparisleri.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.RestoranMenuUrunleri.Entities;

public class RestoranMenuUrun : BaseEntity<int>
{
    public int RestoranMenuKategoriId { get; set; }

    [Required]
    [MaxLength(128)]
    public string Ad { get; set; } = string.Empty;

    [MaxLength(512)]
    public string? Aciklama { get; set; }

    public decimal Fiyat { get; set; }

    [Required]
    [MaxLength(3)]
    public string ParaBirimi { get; set; } = "TRY";

    public int HazirlamaSuresiDakika { get; set; }

    public bool AktifMi { get; set; } = true;

    public RestoranMenuKategori? RestoranMenuKategori { get; set; }

    public ICollection<RestoranSiparisKalemi> SiparisKalemleri { get; set; } = [];
}
