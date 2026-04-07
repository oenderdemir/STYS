using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Kamp.Entities;

public class KampProgramiParametreAyari : BaseEntity<int>
{
    public int KampProgramiId { get; set; }

    public decimal? KamuAvansKisiBasi { get; set; }

    public decimal? DigerAvansKisiBasi { get; set; }

    public int? VazgecmeIadeGunSayisi { get; set; }

    public decimal? GecBildirimGunlukKesintiyUzdesi { get; set; }

    public int? NoShowSuresiGun { get; set; }

    public bool AktifMi { get; set; } = true;

    public KampProgrami? KampProgrami { get; set; }
}
