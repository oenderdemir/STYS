using STYS.Tesisler.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.KonaklamaTipleri.Entities;

public class TesisKonaklamaTipiIcerikOverride : BaseEntity<int>
{
    public int TesisId { get; set; }

    public int KonaklamaTipiIcerikKalemiId { get; set; }

    public bool DevreDisiMi { get; set; }

    public int? Miktar { get; set; }

    public string? Periyot { get; set; }

    public string? KullanimTipi { get; set; }

    public string? KullanimNoktasi { get; set; }

    public TimeSpan? KullanimBaslangicSaati { get; set; }

    public TimeSpan? KullanimBitisSaati { get; set; }

    public bool? CheckInGunuGecerliMi { get; set; }

    public bool? CheckOutGunuGecerliMi { get; set; }

    public string? Aciklama { get; set; }

    public Tesis? Tesis { get; set; }

    public KonaklamaTipiIcerikKalemi? KonaklamaTipiIcerikKalemi { get; set; }
}
