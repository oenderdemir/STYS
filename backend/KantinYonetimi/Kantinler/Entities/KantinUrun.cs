using System.ComponentModel.DataAnnotations;
using STYS.Muhasebe.TasinirKartlari.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.KantinYonetimi.Kantinler.Entities;

public class KantinUrun : BaseEntity<int>
{
    public int KantinId { get; set; }
    public int TasinirKartId { get; set; }
    public int? SiraNo { get; set; }

    [MaxLength(128)]
    public string? Barkod { get; set; }

    public decimal SatisFiyati { get; set; }

    public bool AktifMi { get; set; } = true;

    [MaxLength(1024)]
    public string? Aciklama { get; set; }

    public Kantin? Kantin { get; set; }
    public TasinirKart? TasinirKart { get; set; }
}
