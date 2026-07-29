using System.ComponentModel.DataAnnotations;
using STYS.Muhasebe.KasaBankaHesaplari.Entities;
using STYS.Tesisler.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Entegrasyonlar.Pavo.Entities;

public class PavoTerminal : BaseEntity<int>, ITenantEntity
{
    public int KurumId { get; set; }
    public int TesisId { get; set; }
    public int KasaBankaHesapId { get; set; }

    [Required, MaxLength(128)]
    public string Ad { get; set; } = string.Empty;

    [Required, MaxLength(64)]
    public string SerialNumber { get; set; } = string.Empty;

    [Required, MaxLength(128)]
    public string SourceFingerprint { get; set; } = string.Empty;

    [MaxLength(128)]
    public string? SourceTerminalReference { get; set; }

    [MaxLength(256)]
    public string? TargetFingerprint { get; set; }

    public long? PairingId { get; set; }

    [MaxLength(32)]
    public string? PairingCode { get; set; }

    public bool EslesmeOnayliMi { get; set; }
    public bool AktifMi { get; set; } = true;

    public Tesis? Tesis { get; set; }
    public KasaBankaHesap? KasaBankaHesap { get; set; }
}
