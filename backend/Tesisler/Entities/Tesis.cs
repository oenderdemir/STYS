using System.ComponentModel.DataAnnotations;
using STYS.Binalar.Entities;
using STYS.Iller.Entities;
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

    public bool AktifMi { get; set; } = true;

    public Il? Il { get; set; }

    public ICollection<TesisYonetici> Yoneticiler { get; set; } = [];

    public ICollection<TesisResepsiyonist> Resepsiyonistler { get; set; } = [];

    public ICollection<Bina> Binalar { get; set; } = [];

    public ICollection<OdaTipi> OdaTipleri { get; set; } = [];
}
