using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Kamp.Entities;

public class KampProgramiBasvuruSahibiTipKurali : BaseEntity<int>
{
    public int KampProgramiId { get; set; }

    public int KampBasvuruSahibiTipiId { get; set; }

    public int OncelikSirasi { get; set; }

    public int TabanPuan { get; set; }

    public bool HizmetYiliPuaniAktifMi { get; set; }

    public int EmekliBonusPuani { get; set; }

    public string? VarsayilanKatilimciTipiKodu { get; set; }

    public bool AktifMi { get; set; } = true;

    public KampProgrami? KampProgrami { get; set; }

    public KampBasvuruSahibiTipi? KampBasvuruSahibiTipi { get; set; }
}
