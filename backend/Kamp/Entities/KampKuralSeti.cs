using System.ComponentModel.DataAnnotations;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Kamp.Entities;

public class KampKuralSeti : BaseEntity<int>
{
    public int KampYili { get; set; }

    public int OncekiYilSayisi { get; set; }

    public int KatilimCezaPuani { get; set; }

    public bool AktifMi { get; set; } = true;
}
