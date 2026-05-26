using System.ComponentModel.DataAnnotations;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;
using STYS.Tesisler.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Muhasebe.TevkifatHesapEslemeleri.Entities;

public class TevkifatHesapEsleme : BaseEntity<int>
{
    public int? TesisId { get; set; }

    [Required]
    [MaxLength(16)]
    public string IslemYonu { get; set; } = string.Empty;

    public int TevkifatPay { get; set; }

    public int TevkifatPayda { get; set; } = 10;

    public int MuhasebeHesapPlaniId { get; set; }

    public bool AktifMi { get; set; } = true;

    [MaxLength(1024)]
    public string? Aciklama { get; set; }

    public Tesis? Tesis { get; set; }
    public MuhasebeHesapPlani? MuhasebeHesapPlani { get; set; }
}
