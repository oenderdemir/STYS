using System.ComponentModel.DataAnnotations;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Kamp.Entities;

public class KampBasvuru : BaseEntity<int>
{
    public int KampDonemiId { get; set; }

    public int TesisId { get; set; }

    [Required]
    [MaxLength(32)]
    public string KonaklamaBirimiTipi { get; set; } = string.Empty;

    [Required]
    [MaxLength(32)]
    public string BasvuruNo { get; set; } = string.Empty;

    public int KampBasvuruSahibiId { get; set; }

    [Required]
    [MaxLength(200)]
    public string BasvuruSahibiAdiSoyadiSnapshot { get; set; } = string.Empty;

    [Required]
    [MaxLength(32)]
    public string BasvuruSahibiTipiSnapshot { get; set; } = string.Empty;

    public int HizmetYiliSnapshot { get; set; }

    public bool EvcilHayvanGetirecekMi { get; set; }

    [MaxLength(32)]
    public string Durum { get; set; } = KampBasvuruDurumlari.Beklemede;

    public int KatilimciSayisi { get; set; }

    public int OncelikSirasi { get; set; }

    public int Puan { get; set; }

    public decimal GunlukToplamTutar { get; set; }

    public decimal DonemToplamTutar { get; set; }

    public decimal AvansToplamTutar { get; set; }

    public decimal KalanOdemeTutari { get; set; }

    [MaxLength(2048)]
    public string? UyariMesajlariJson { get; set; }

    public bool BuzdolabiTalepEdildiMi { get; set; }

    public bool TelevizyonTalepEdildiMi { get; set; }

    public bool KlimaTalepEdildiMi { get; set; }

    public KampDonemi? KampDonemi { get; set; }

    public KampBasvuruSahibi? KampBasvuruSahibi { get; set; }

    public Tesisler.Entities.Tesis? Tesis { get; set; }

    public ICollection<KampBasvuruKatilimci> Katilimcilar { get; set; } = [];

    public ICollection<KampBasvuruTercih> Tercihler { get; set; } = [];
}
