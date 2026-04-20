using System.ComponentModel.DataAnnotations;
using STYS.Muhasebe.BankaHareketleri.Entities;
using STYS.Muhasebe.CariHareketler.Entities;
using STYS.Muhasebe.KasaHareketleri.Entities;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Entities;
using STYS.Tesisler.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Muhasebe.CariKartlar.Entities;

public class CariKart : BaseEntity<int>
{
    public int? TesisId { get; set; }

    [Required]
    [MaxLength(32)]
    public string CariTipi { get; set; } = CariKartTipleri.Musteri;

    [Required]
    [MaxLength(64)]
    public string CariKodu { get; set; } = string.Empty;

    [Required]
    [MaxLength(256)]
    public string UnvanAdSoyad { get; set; } = string.Empty;

    [MaxLength(32)]
    public string? VergiNoTckn { get; set; }

    [MaxLength(128)]
    public string? VergiDairesi { get; set; }

    [MaxLength(32)]
    public string? Telefon { get; set; }

    [MaxLength(256)]
    public string? Eposta { get; set; }

    [MaxLength(512)]
    public string? Adres { get; set; }

    [MaxLength(128)]
    public string? Il { get; set; }

    [MaxLength(128)]
    public string? Ilce { get; set; }

    public bool AktifMi { get; set; } = true;

    public bool EFaturaMukellefiMi { get; set; }

    public bool EArsivKapsamindaMi { get; set; }

    [MaxLength(1024)]
    public string? Aciklama { get; set; }

    public ICollection<CariHareket> CariHareketler { get; set; } = [];
    public ICollection<KasaHareket> KasaHareketler { get; set; } = [];
    public ICollection<BankaHareket> BankaHareketler { get; set; } = [];
    public ICollection<TahsilatOdemeBelgesi> TahsilatOdemeBelgeleri { get; set; } = [];
    public Tesis? Tesis { get; set; }
}

