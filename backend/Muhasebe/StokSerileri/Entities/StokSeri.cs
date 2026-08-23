using System.ComponentModel.DataAnnotations;
using STYS.Muhasebe.StokHareketleri.Entities;
using STYS.Muhasebe.TasinirKartlari.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Muhasebe.StokSerileri.Entities;

public class StokSeri : BaseEntity<int>
{
    public int TesisId { get; set; }

    public int TasinirKartId { get; set; }

    [Required]
    [MaxLength(128)]
    public string SeriNo { get; set; } = string.Empty;

    public bool AktifMi { get; set; } = true;

    [MaxLength(1024)]
    public string? Aciklama { get; set; }

    public TasinirKart? TasinirKart { get; set; }
    public ICollection<StokHareket> StokHareketleri { get; set; } = [];
}
