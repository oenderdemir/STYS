using System.ComponentModel.DataAnnotations;
using STYS.Binalar.Entities;
using STYS.OdaTipleri.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Odalar.Entities;

public class Oda : BaseEntity<int>
{
    [Required]
    [MaxLength(64)]
    public string OdaNo { get; set; } = string.Empty;

    public int BinaId { get; set; }

    public int OdaTipiId { get; set; }

    public int KatNo { get; set; }

    public int? YatakSayisi { get; set; }

    public bool AktifMi { get; set; } = true;

    public Bina? Bina { get; set; }

    public OdaTipi? OdaTipi { get; set; }
}