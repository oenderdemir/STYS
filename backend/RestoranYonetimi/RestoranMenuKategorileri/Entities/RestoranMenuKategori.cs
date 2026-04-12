using System.ComponentModel.DataAnnotations;
using STYS.Restoranlar.Entities;
using STYS.RestoranMenuUrunleri.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.RestoranMenuKategorileri.Entities;

public class RestoranMenuKategori : BaseEntity<int>
{
    public int RestoranId { get; set; }

    [Required]
    [MaxLength(128)]
    public string Ad { get; set; } = string.Empty;

    public int SiraNo { get; set; }

    public bool AktifMi { get; set; } = true;

    public Restoran? Restoran { get; set; }

    public ICollection<RestoranMenuUrun> Urunler { get; set; } = [];
}
