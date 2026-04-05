using System.ComponentModel.DataAnnotations;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Kamp.Entities;

public class KampDonemi : BaseEntity<int>
{
    public int KampProgramiId { get; set; }

    [Required]
    [MaxLength(64)]
    public string Kod { get; set; } = string.Empty;

    [Required]
    [MaxLength(160)]
    public string Ad { get; set; } = string.Empty;

    public int Yil { get; set; }

    public DateTime BasvuruBaslangicTarihi { get; set; }

    public DateTime BasvuruBitisTarihi { get; set; }

    public DateTime KonaklamaBaslangicTarihi { get; set; }

    public DateTime KonaklamaBitisTarihi { get; set; }

    public int MinimumGece { get; set; } = 1;

    public int MaksimumGece { get; set; } = 1;

    public bool OnayGerektirirMi { get; set; } = true;

    public bool CekilisGerekliMi { get; set; }

    public bool AyniAileIcinTekBasvuruMu { get; set; } = true;

    public DateTime? IptalSonGun { get; set; }

    public bool AktifMi { get; set; } = true;

    public KampProgrami? KampProgrami { get; set; }

    public ICollection<KampDonemiTesis> TesisAtamalari { get; set; } = [];

    public ICollection<KampBasvuru> Basvurular { get; set; } = [];
}
