using System.ComponentModel.DataAnnotations;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Kamp.Entities;

public class KampBasvuruKatilimci : BaseEntity<int>
{
    public int KampBasvuruId { get; set; }

    [Required]
    [MaxLength(200)]
    public string AdSoyad { get; set; } = string.Empty;

    [MaxLength(32)]
    public string? TcKimlikNo { get; set; }

    public DateTime DogumTarihi { get; set; }

    public bool BasvuruSahibiMi { get; set; }

    [Required]
    [MaxLength(32)]
    public string KatilimciTipi { get; set; } = KampKatilimciTipleri.Kamu;

    [Required]
    [MaxLength(32)]
    public string AkrabalikTipi { get; set; } = KampAkrabalikTipleri.Diger;

    public bool KimlikBilgileriDogrulandiMi { get; set; }

    public bool YemekTalepEdiyorMu { get; set; } = true;

    public KampBasvuru? KampBasvuru { get; set; }
}
