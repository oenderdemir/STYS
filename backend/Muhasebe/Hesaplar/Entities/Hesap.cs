using System.ComponentModel.DataAnnotations;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Muhasebe.Hesaplar.Entities;

public class Hesap : BaseEntity<int>
{
    [Required]
    [MaxLength(128)]
    public string Ad { get; set; } = string.Empty;

    public int MuhasebeHesapPlaniId { get; set; }

    public bool GenelHesapMi { get; set; }

    [MaxLength(64)]
    public string? MuhasebeFormu { get; set; }

    public bool AktifMi { get; set; } = true;

    [MaxLength(1024)]
    public string? Aciklama { get; set; }

    public MuhasebeHesapPlani? MuhasebeHesapPlani { get; set; }
    public ICollection<HesapKasaBankaBaglanti> KasaBankaBaglantilari { get; set; } = [];
    public ICollection<HesapDepoBaglanti> DepoBaglantilari { get; set; } = [];
}
