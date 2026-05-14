using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Muhasebe.MuhasebeFisleri.Entities;

/// <summary>
/// Muhasebe fişi satırı. Her satır bir muhasebe hesabına borç veya alacak yazar.
/// </summary>
public class MuhasebeFisSatir : BaseEntity<int>
{
    public int MuhasebeFisId { get; set; }
    public int MuhasebeHesapPlaniId { get; set; }

    public int SiraNo { get; set; }

    public decimal Borc { get; set; }
    public decimal Alacak { get; set; }

    public string ParaBirimi { get; set; } = "TRY";
    public decimal Kur { get; set; } = 1;

    public int? CariKartId { get; set; }
    public int? TasinirKartId { get; set; }
    public int? DepoId { get; set; }
    public int? KasaBankaHesapId { get; set; }

    public string? Aciklama { get; set; }

    // Navigation properties
    public MuhasebeFis? MuhasebeFis { get; set; }
    public MuhasebeHesapPlani? MuhasebeHesapPlani { get; set; }
}
