using System.ComponentModel.DataAnnotations;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Kbs.Entities;

public class KbsDurumGecmisi : BaseEntity<long>, ITenantEntity
{
    public int KurumId { get; set; }
    public long KbsBildirimId { get; set; }
    [MaxLength(32)] public string OncekiDurum { get; set; } = string.Empty;
    [MaxLength(32)] public string YeniDurum { get; set; } = string.Empty;
    [MaxLength(32)] public string IslemTipi { get; set; } = string.Empty;
    [MaxLength(512)] public string Aciklama { get; set; } = string.Empty;
    [MaxLength(128)] public string? KurumReferansNo { get; set; }
    public Guid? IslemYapanUserId { get; set; }
    [MaxLength(256)] public string? IslemYapanUserAdi { get; set; }
    public DateTime IslemTarihi { get; set; }
    public KbsBildirim? Bildirim { get; set; }
}
