using System.ComponentModel.DataAnnotations;
using STYS.Fiyatlandirma.Entities;
using STYS.Tesisler.Entities;
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

    public ICollection<KonaklamaTipiIcerikKalemi> IcerikKalemleri { get; set; } = [];

    public ICollection<OdaFiyat> OdaFiyatlari { get; set; } = [];

    public ICollection<IndirimKuraliKonaklamaTipi> IndirimKuralKonaklamaTipleri { get; set; } = [];

    public ICollection<TesisKonaklamaTipi> TesisKonaklamaTipleri { get; set; } = [];
}
