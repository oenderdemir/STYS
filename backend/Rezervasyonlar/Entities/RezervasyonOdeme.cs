using System.ComponentModel.DataAnnotations;
using STYS.Muhasebe.KasaBankaHesaplari.Entities;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Rezervasyonlar.Entities;

public class RezervasyonOdeme : BaseEntity<int>
{
    public int RezervasyonId { get; set; }

    public DateTime OdemeTarihi { get; set; } = DateTime.UtcNow;

    public decimal OdemeTutari { get; set; }

    [Required]
    [MaxLength(3)]
    public string ParaBirimi { get; set; } = "TRY";

    [Required]
    [MaxLength(32)]
    public string OdemeTipi { get; set; } = OdemeTipleri.Nakit;

    [MaxLength(512)]
    public string? Aciklama { get; set; }

    /// <summary>Nakit hareketi doguran odeme tiplerinde zorunlu (bkz. OdemeYontemleri.NakitHareketiGerektirenler).</summary>
    public int? KasaBankaHesapId { get; set; }

    /// <summary>Bu odeme icin uretilen muhasebe tahsilat belgesi. Dogrudan MuhasebeFisId tutulmaz;
    /// fise ihtiyac duyan akislar TahsilatOdemeBelgesi.MuhasebeFisId uzerinden erisir.</summary>
    public int? TahsilatOdemeBelgesiId { get; set; }

    [Required]
    [MaxLength(16)]
    public string Durum { get; set; } = RezervasyonOdemeDurumlari.Aktif;

    public DateTime? IptalTarihi { get; set; }

    [MaxLength(512)]
    public string? IptalAciklama { get; set; }

    public Rezervasyon? Rezervasyon { get; set; }

    public KasaBankaHesap? KasaBankaHesap { get; set; }

    public TahsilatOdemeBelgesi? TahsilatOdemeBelgesi { get; set; }
}

