using System.ComponentModel.DataAnnotations;
using STYS.Fiyatlandirma.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.KonaklamaTipleri.Entities;

public class KonaklamaTipi : BaseEntity<int>
{
    [Required]
    [MaxLength(64)]
    public string Kod { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string Ad { get; set; } = string.Empty;

    public bool AktifMi { get; set; } = true;

    public ICollection<OdaFiyat> OdaFiyatlari { get; set; } = [];

    public ICollection<IndirimKuraliKonaklamaTipi> IndirimKuralKonaklamaTipleri { get; set; } = [];
}
