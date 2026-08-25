using System.ComponentModel.DataAnnotations;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.Depolar.Entities;
using STYS.Tesisler.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.KantinYonetimi.Kantinler.Entities;

public class Kantin : BaseEntity<int>
{
    public int TesisId { get; set; }
    public int DepoId { get; set; }
    public int? PerakendeCariKartId { get; set; }

    [Required]
    [MaxLength(64)]
    public string Kod { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Ad { get; set; } = string.Empty;

    public bool AktifMi { get; set; } = true;

    [MaxLength(1024)]
    public string? Aciklama { get; set; }

    public Tesis? Tesis { get; set; }
    public Depo? Depo { get; set; }
    public CariKart? PerakendeCariKart { get; set; }
    public ICollection<KantinUrun> Urunler { get; set; } = [];
    public ICollection<KantinSatisNoktasi> SatisNoktalari { get; set; } = [];
}
