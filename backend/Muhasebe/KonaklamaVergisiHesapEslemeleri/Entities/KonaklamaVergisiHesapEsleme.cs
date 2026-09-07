using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;
using STYS.Tesisler.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Muhasebe.KonaklamaVergisiHesapEslemeleri.Entities;

public class KonaklamaVergisiHesapEsleme : BaseEntity<int>
{
    public int? TesisId { get; set; }
    public int VergiHesapId { get; set; }
    public bool AktifMi { get; set; } = true;
    public string? Aciklama { get; set; }

    public Tesis? Tesis { get; set; }
    public MuhasebeHesapPlani? VergiHesap { get; set; }
}

