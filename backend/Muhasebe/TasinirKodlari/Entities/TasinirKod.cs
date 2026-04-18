using System.ComponentModel.DataAnnotations;
using STYS.Muhasebe.TasinirKartlari.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Muhasebe.TasinirKodlari.Entities;

public class TasinirKod : BaseEntity<int>
{
    [Required]
    [MaxLength(64)]
    public string TamKod { get; set; } = string.Empty;

    [Required]
    [MaxLength(16)]
    public string Kod { get; set; } = string.Empty;

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
