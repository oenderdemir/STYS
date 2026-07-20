using System.ComponentModel.DataAnnotations;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Kbs.Entities;

public class KbsTesisAyari : BaseEntity<int>, ITenantEntity
{
    public int KurumId { get; set; }
    public int TesisId { get; set; }
    [MaxLength(16)] public string KollukSistemi { get; set; } = string.Empty;
    [MaxLength(16)] public string EntegrasyonTipi { get; set; } = string.Empty;
    [MaxLength(64)] public string? TesisKodu { get; set; }
    [MaxLength(256)] public string? SecretReference { get; set; }
    public bool AktifMi { get; set; }
    public bool CanliGonderimAktifMi { get; set; }
    public DateTime? SonBaglantiKontrolTarihi { get; set; }
    [MaxLength(512)] public string? SonBaglantiKontrolSonucu { get; set; }
}
