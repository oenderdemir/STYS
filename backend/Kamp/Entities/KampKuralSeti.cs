using System.ComponentModel.DataAnnotations;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Kamp.Entities;

public class KampKuralSeti : BaseEntity<int>
{
    public int KampProgramiId { get; set; }

    public int OncekiYilSayisi { get; set; }

    public int KatilimCezaPuani { get; set; }

    public int KatilimciBasinaPuan { get; set; } = 10;

    public bool AktifMi { get; set; } = true;

    public KampProgrami? KampProgrami { get; set; }
}
