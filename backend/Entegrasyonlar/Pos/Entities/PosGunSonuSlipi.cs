using System.ComponentModel.DataAnnotations;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Entegrasyonlar.Pos.Entities;

public enum PosGunSonuSlipTipi
{
    EodImage = 1
}

/// <summary>
/// Gün sonu slip görseli. Görsel güvenli filesystem'de saklanır; bu entity yalnız metadata + göreli
/// StoragePath tutar. StoragePath backend-internal'dır ve hiçbir API/DTO'ya çıkmaz.
/// </summary>
public class PosGunSonuSlipi : BaseEntity<int>, ITenantEntity
{
    public int KurumId { get; set; }
    public int TesisId { get; set; }

    public int PosGunSonuIslemiId { get; set; }
    public PosGunSonuIslemi? PosGunSonuIslemi { get; set; }

    public int PosCihaziId { get; set; }

    public PosGunSonuSlipTipi SlipTipi { get; set; }

    [MaxLength(64)]
    public string ContentType { get; set; } = "image/png";

    [MaxLength(1024)]
    public string StoragePath { get; set; } = string.Empty;

    [MaxLength(64)]
    public string Sha256 { get; set; } = string.Empty;

    public long DosyaBoyutu { get; set; }

    public DateTime OlusturulmaTarihi { get; set; }

    [MaxLength(256)]
    public string? Aciklama { get; set; }
}
