using System.ComponentModel.DataAnnotations;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;

public class MuhasebeHesapPlani : BaseEntity<int>
{
    [Required]
    [MaxLength(16)]
    public string Kod { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    public string TamKod { get; set; } = string.Empty;

    [Required]
    [MaxLength(256)]
    public string Ad { get; set; } = string.Empty;

    public int SeviyeNo { get; set; }

    public int? UstHesapId { get; set; }

    public bool AktifMi { get; set; } = true;

    [MaxLength(1024)]
    public string? Aciklama { get; set; }

    public MuhasebeHesapPlani? UstHesap { get; set; }
    public ICollection<MuhasebeHesapPlani> AltHesaplar { get; set; } = [];
}
