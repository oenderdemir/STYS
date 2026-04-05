using System.ComponentModel.DataAnnotations;
using STYS.Binalar.Entities;
using STYS.Iller.Entities;
using STYS.Kamp.Entities;
using STYS.KonaklamaTipleri.Entities;
using STYS.MisafirTipleri.Entities;
using STYS.OdaTipleri.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Tesisler.Entities;

public class Tesis : BaseEntity<int>
{
    [Required]
    [MaxLength(200)]
    public string Ad { get; set; } = string.Empty;

    public int IlId { get; set; }

    [Required]
    [MaxLength(32)]
    public string Telefon { get; set; } = string.Empty;

    [Required]
    [MaxLength(512)]
    public string Adres { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? Eposta { get; set; }

    public TimeSpan GirisSaati { get; set; } = new(14, 0, 0);

    public TimeSpan CikisSaati { get; set; } = new(10, 0, 0);

    [Required]
    [MaxLength(16)]
    public string EkHizmetPaketCakismaPolitikasi { get; set; } = EkHizmetPaketCakismaPolitikalari.OnayIste;

    public bool AktifMi { get; set; } = true;

    public Il? Il { get; set; }

    public ICollection<TesisYonetici> Yoneticiler { get; set; } = [];

    public ICollection<TesisResepsiyonist> Resepsiyonistler { get; set; } = [];

    public ICollection<Bina> Binalar { get; set; } = [];

    public ICollection<OdaTipi> OdaTipleri { get; set; } = [];

    public ICollection<TesisKonaklamaTipi> KonaklamaTipleri { get; set; } = [];

    public ICollection<TesisMisafirTipi> MisafirTipleri { get; set; } = [];

    public ICollection<TesisKonaklamaTipiIcerikOverride> KonaklamaTipiIcerikOverridelari { get; set; } = [];

    public ICollection<KampDonemiTesis> KampDonemiTesisleri { get; set; } = [];

    public ICollection<KampBasvuru> KampBasvurulari { get; set; } = [];
}
