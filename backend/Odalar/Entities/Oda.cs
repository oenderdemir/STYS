using System.ComponentModel.DataAnnotations;
using STYS.Odalar;
using STYS.Binalar.Entities;
using STYS.OdaOzellikleri.Entities;
using STYS.OdaTipleri.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Odalar.Entities;

public class Oda : BaseEntity<int>
{
    [Required]
    [MaxLength(64)]
    public string OdaNo { get; set; } = string.Empty;

    public int BinaId { get; set; }

    public int TesisOdaTipiId { get; set; }

    public int KatNo { get; set; }

    public bool AktifMi { get; set; } = true;

    [Required]
    [MaxLength(32)]
    public string TemizlikDurumu { get; set; } = OdaTemizlikDurumlari.Hazir;

    public Bina? Bina { get; set; }

    public OdaTipi? TesisOdaTipi { get; set; }

    public ICollection<OdaOzellikDeger> OdaOzellikDegerleri { get; set; } = [];
}
