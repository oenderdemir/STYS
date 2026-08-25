using System.ComponentModel.DataAnnotations;
using STYS.Muhasebe.KasaBankaHesaplari.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.KantinYonetimi.Kantinler.Entities;

public class KantinSatisNoktasi : BaseEntity<int>
{
    public int KantinId { get; set; }

    [Required]
    [MaxLength(64)]
    public string Kod { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Ad { get; set; } = string.Empty;

    public int? VarsayilanNakitKasaId { get; set; }
    public int? VarsayilanPosHesapId { get; set; }

    public bool VarsayilanMi { get; set; }
    public bool AktifMi { get; set; } = true;

    [MaxLength(1024)]
    public string? Aciklama { get; set; }

    public Kantin? Kantin { get; set; }
    public KasaBankaHesap? VarsayilanNakitKasa { get; set; }
    public KasaBankaHesap? VarsayilanPosHesap { get; set; }
}
