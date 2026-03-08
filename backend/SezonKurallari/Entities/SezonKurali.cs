using System.ComponentModel.DataAnnotations;
using STYS.Tesisler.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.SezonKurallari.Entities;

public class SezonKurali : BaseEntity<int>
{
    public int TesisId { get; set; }

    [Required]
    [MaxLength(64)]
    public string Kod { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Ad { get; set; } = string.Empty;

    public DateTime BaslangicTarihi { get; set; }

    public DateTime BitisTarihi { get; set; }

    public int MinimumGece { get; set; } = 1;

    public bool StopSaleMi { get; set; }

    public bool AktifMi { get; set; } = true;

    public Tesis? Tesis { get; set; }
}
