using STYS.Kamp.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Kamp.Entities;

public class KampKonaklamaTarifesi : BaseEntity<int>
{
    public int KampProgramiId { get; set; }

    public string Kod { get; set; } = string.Empty;

    public string Ad { get; set; } = string.Empty;

    public int MinimumKisi { get; set; }

    public int MaksimumKisi { get; set; }

    public decimal KamuGunlukUcret { get; set; }

    public decimal DigerGunlukUcret { get; set; }

    public decimal BuzdolabiGunlukUcret { get; set; }

    public decimal TelevizyonGunlukUcret { get; set; }

    public decimal KlimaGunlukUcret { get; set; }

    public bool AktifMi { get; set; } = true;

    public KampProgrami? KampProgrami { get; set; }
}
