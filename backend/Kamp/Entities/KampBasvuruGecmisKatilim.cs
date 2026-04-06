using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Kamp.Entities;

public class KampBasvuruGecmisKatilim : BaseEntity<int>
{
    public int KampBasvuruSahibiId { get; set; }

    public int KatilimYili { get; set; }

    public int? KaynakBasvuruId { get; set; }

    public bool BeyanMi { get; set; } = true;

    public bool AktifMi { get; set; } = true;

    public KampBasvuruSahibi? KampBasvuruSahibi { get; set; }

    public KampBasvuru? KaynakBasvuru { get; set; }
}
