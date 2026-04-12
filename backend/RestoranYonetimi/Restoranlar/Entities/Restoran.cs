using System.ComponentModel.DataAnnotations;
using STYS.IsletmeAlanlari.Entities;
using STYS.RestoranMasalari.Entities;
using STYS.RestoranMenuKategorileri.Entities;
using STYS.RestoranSiparisleri.Entities;
using STYS.Tesisler.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Restoranlar.Entities;

public class Restoran : BaseEntity<int>
{
    public int TesisId { get; set; }
    public int? IsletmeAlaniId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Ad { get; set; } = string.Empty;

    [MaxLength(512)]
    public string? Aciklama { get; set; }

    public bool AktifMi { get; set; } = true;

    public Tesis? Tesis { get; set; }
    public IsletmeAlani? IsletmeAlani { get; set; }
    public ICollection<RestoranYonetici> Yoneticiler { get; set; } = [];

    public ICollection<RestoranMasa> Masalar { get; set; } = [];

    public ICollection<RestoranMenuKategori> MenuKategorileri { get; set; } = [];

    public ICollection<RestoranSiparis> Siparisler { get; set; } = [];
}
