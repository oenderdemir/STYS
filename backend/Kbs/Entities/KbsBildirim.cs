using System.ComponentModel.DataAnnotations;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Kbs.Entities;

public class KbsBildirim : BaseEntity<long>, ITenantEntity
{
    public int KurumId { get; set; }
    public int TesisId { get; set; }
    public int RezervasyonId { get; set; }
    public int RezervasyonKonaklayanId { get; set; }
    [MaxLength(24)] public string BildirimTipi { get; set; } = string.Empty;
    [MaxLength(32)] public string Saglayici { get; set; } = string.Empty;
    [MaxLength(32)] public string Durum { get; set; } = string.Empty;
    [MaxLength(128)] public string IdempotencyKey { get; set; } = string.Empty;
    [MaxLength(64)] public string OlayAnahtari { get; set; } = string.Empty;
    [MaxLength(16)] public string PayloadVersion { get; set; } = "1";
    [MaxLength(64)] public string PayloadHash { get; set; } = string.Empty;
    public int DenemeSayisi { get; set; }
    public DateTime? SonrakiDenemeTarihi { get; set; }
    [MaxLength(64)] public string? SonHataKodu { get; set; }
    [MaxLength(512)] public string? SonHataMesaji { get; set; }
    public DateTime? GonderimTarihi { get; set; }
    public DateTime? TamamlanmaTarihi { get; set; }
    [MaxLength(128)] public string? ExcelManifestHash { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = [];
}
