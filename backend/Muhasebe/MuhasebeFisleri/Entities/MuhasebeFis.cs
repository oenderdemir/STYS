using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Muhasebe.MuhasebeFisleri.Entities;

/// <summary>
/// Muhasebe fişi ana kaydı. Manuel veya otomatik oluşturulabilir.
/// </summary>
public class MuhasebeFis : BaseEntity<int>
{
    public int TesisId { get; set; }

    public int MaliYil { get; set; }
    public int Donem { get; set; }

    public string FisNo { get; set; } = string.Empty;
    public int? YevmiyeNo { get; set; }

    public DateTime FisTarihi { get; set; }

    public string FisTipi { get; set; } = string.Empty;
    public string KaynakModul { get; set; } = string.Empty;
    public int? KaynakId { get; set; }

    public string Durum { get; set; } = string.Empty;

    public decimal ToplamBorc { get; set; }
    public decimal ToplamAlacak { get; set; }

    public string? Aciklama { get; set; }

    public int? TersKayitFisId { get; set; }
    public int? IptalEdilenFisId { get; set; }

    public MuhasebeFis? TersKayitFis { get; set; }
    public MuhasebeFis? IptalEdilenFis { get; set; }

    public ICollection<MuhasebeFisSatir> Satirlar { get; set; } = new List<MuhasebeFisSatir>();
}
