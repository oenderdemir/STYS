using System.ComponentModel.DataAnnotations;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Rezervasyonlar.Entities;

public class RezervasyonDegisiklikGecmisi : BaseEntity<int>
{
    public int RezervasyonId { get; set; }

    [Required]
    [MaxLength(64)]
    public string IslemTipi { get; set; } = string.Empty;

    [MaxLength(512)]
    public string? Aciklama { get; set; }

    public string? OncekiDegerJson { get; set; }

    public string? YeniDegerJson { get; set; }

    public Rezervasyon? Rezervasyon { get; set; }
}
