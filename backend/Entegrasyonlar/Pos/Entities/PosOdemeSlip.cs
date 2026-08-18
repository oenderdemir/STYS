using System.ComponentModel.DataAnnotations;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Entegrasyonlar.Pos.Entities;

public enum PosOdemeSlipTipi
{
    Customer = 1,
    Merchant = 2,
    Error = 3
}

/// <summary>
/// A persisted PAVO payment receipt slip (customer / merchant / error). The raw image is stored as a
/// file under the secure receipt storage root; this row holds only metadata + the relative path. Raw
/// Base64 is never kept here. One logical current record per (PosOdemeIslemiId, Tip).
/// </summary>
public class PosOdemeSlip : BaseEntity<int>, ITenantEntity
{
    public int KurumId { get; set; }
    public int TesisId { get; set; }
    public int PosOdemeIslemiId { get; set; }

    public PosOdemeSlipTipi Tip { get; set; }

    [MaxLength(64)]
    public string ContentType { get; set; } = "image/png";

    [MaxLength(1024)]
    public string StoragePath { get; set; } = string.Empty;

    public long DosyaBoyutu { get; set; }

    [MaxLength(64)]
    public string Sha256 { get; set; } = string.Empty;

    public DateTime KaydedilmeTarihi { get; set; }

    /// <summary>Source command that produced the slip: "PavoStartPayment" or "PavoGetPaymentResult".
    /// Useful for diagnosing whether a slip arrived during the initial payment or a recovery query.</summary>
    [MaxLength(64)]
    public string? KaynakKomutTipi { get; set; }

    public PosOdemeIslemi? PosOdemeIslemi { get; set; }
}
