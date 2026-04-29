using System.ComponentModel.DataAnnotations;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;
using STYS.Muhasebe.StokHareketleri.Entities;
using STYS.Muhasebe.TasinirKodlari.Entities;
using STYS.Tesisler.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Muhasebe.TasinirKartlari.Entities;

public class TasinirKart : BaseEntity<int>
{
    public int? TesisId { get; set; }

    public int TasinirKodId { get; set; }
    public int? MuhasebeHesapPlaniId { get; set; }
    public string? AnaMuhasebeHesapKodu { get; set; }
    public int? MuhasebeHesapSiraNo { get; set; }

    [Required]
    [MaxLength(64)]
    public string StokKodu { get; set; } = string.Empty;

    [Required]
    [MaxLength(256)]
    public string Ad { get; set; } = string.Empty;

    [Required]
    [MaxLength(32)]
    public string Birim { get; set; } = "Adet";

    [Required]
    [MaxLength(32)]
    public string MalzemeTipi { get; set; } = MalzemeTipleri.Diger;

    public bool SarfMi { get; set; }

    public bool DemirbasMi { get; set; }

    public bool TakipliMi { get; set; }

    public decimal KdvOrani { get; set; }

    public bool AktifMi { get; set; } = true;

    [MaxLength(1024)]
    public string? Aciklama { get; set; }

    public Tesis? Tesis { get; set; }
    public TasinirKod? TasinirKod { get; set; }
    public MuhasebeHesapPlani? MuhasebeHesapPlani { get; set; }
    public ICollection<StokHareket> StokHareketleri { get; set; } = [];
}
