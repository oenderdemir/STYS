using System.ComponentModel.DataAnnotations;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Bildirimler.Entities;

public class BildirimTercih : BaseEntity<int>
{
    public Guid UserId { get; set; }

    public bool BildirimlerAktifMi { get; set; } = true;

    [Required]
    [MaxLength(16)]
    public string MinimumSeverity { get; set; } = BildirimSeverityleri.Info;

    public string? IzinliTiplerJson { get; set; }

    public string? IzinliKaynaklarJson { get; set; }
}
