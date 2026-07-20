using System.ComponentModel.DataAnnotations;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Kbs.Entities;

public class KbsBildirimDenemesi : BaseEntity<long>, ITenantEntity
{
    public int KurumId { get; set; }
    public long KbsBildirimId { get; set; }
    public DateTime DenemeTarihi { get; set; }
    [MaxLength(32)] public string Sonuc { get; set; } = string.Empty;
    [MaxLength(32)] public string? HataSinifi { get; set; }
    [MaxLength(64)] public string? SaglayiciHataKodu { get; set; }
    [MaxLength(512)] public string? MaskelenmisAciklama { get; set; }
    public KbsBildirim? Bildirim { get; set; }
}
