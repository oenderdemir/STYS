using System.ComponentModel.DataAnnotations;
using TOD.Platform.Persistence.Rdbms.Dto;

namespace STYS.Kamp.Dto;

public class KampDonemiDto : BaseRdbmsDto<int>
{
    [Required]
    public int KampProgramiId { get; set; }

    public string? KampProgramiAd { get; set; }

    [Required]
    public string Kod { get; set; } = string.Empty;

    [Required]
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
}
