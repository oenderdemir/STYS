using System.ComponentModel.DataAnnotations;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Muhasebe.CariKartlar.Entities;

public class CariKartYetkiliKisi : BaseEntity<int>
{
    public int CariKartId { get; set; }

    [Required]
    [MaxLength(256)]
    public string AdSoyad { get; set; } = string.Empty;

    [MaxLength(128)]
    public string? GorevUnvan { get; set; }

    [MaxLength(32)]
    public string? Telefon { get; set; }

    [MaxLength(256)]
    public string? Eposta { get; set; }

    [MaxLength(1024)]
    public string? Aciklama { get; set; }

    public CariKart? CariKart { get; set; }
}
