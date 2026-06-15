using System.ComponentModel.DataAnnotations;
using STYS.Kurumlar.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Kamp.Entities;

public class KampProgrami : BaseEntity<int>, ITenantEntity
{
    public int KurumId { get; set; }

    [Required]
    [MaxLength(64)]
    public string Kod { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string Ad { get; set; } = string.Empty;

    [MaxLength(512)]
    public string? Aciklama { get; set; }

    public int Yil { get; set; }

    public int MaksimumBasvuruSayisi { get; set; } = 1;

    public bool AktifMi { get; set; } = true;

    public Kurum? Kurum { get; set; }

    public ICollection<KampDonemi> KampDonemleri { get; set; } = [];
}
