using System.ComponentModel.DataAnnotations;
using STYS.Muhasebe.TasinirKartlari.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Muhasebe.TasinirKodlari.Entities;

public class TasinirKod : BaseEntity<int>
{
    [Required]
    [MaxLength(64)]
    public string TamKod { get; set; } = string.Empty;

    [MaxLength(16)]
    public string? Duzey1Kod { get; set; }

    [MaxLength(16)]
    public string? Duzey2Kod { get; set; }

    [MaxLength(16)]
    public string? Duzey3Kod { get; set; }

    [MaxLength(16)]
    public string? Duzey4Kod { get; set; }

    [MaxLength(16)]
    public string? Duzey5Kod { get; set; }

    [Required]
    [MaxLength(256)]
    public string Ad { get; set; } = string.Empty;

    public int DuzeyNo { get; set; }

    public int? UstKodId { get; set; }

    public bool AktifMi { get; set; } = true;

    [MaxLength(1024)]
    public string? Aciklama { get; set; }

    public TasinirKod? UstKod { get; set; }
    public ICollection<TasinirKod> AltKodlar { get; set; } = [];
    public ICollection<TasinirKart> TasinirKartlari { get; set; } = [];
}
