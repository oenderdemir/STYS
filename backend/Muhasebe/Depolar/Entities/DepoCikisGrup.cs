using System.ComponentModel.DataAnnotations;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Muhasebe.Depolar.Entities;

public class DepoCikisGrup : BaseEntity<int>
{
    public int DepoId { get; set; }

    [Required]
    [MaxLength(200)]
    public string CikisGrupAdi { get; set; } = string.Empty;

    public decimal KarOrani { get; set; }

    public int? LokasyonId { get; set; }

    public Depo? Depo { get; set; }
}
