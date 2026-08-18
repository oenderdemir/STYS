using System.ComponentModel.DataAnnotations;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Entegrasyonlar.Pos.Entities;

public enum PosGunSonuDurumu
{
    Pending = 0,
    Successful = 1,
    Failed = 2,
    Unknown = 3
}

/// <summary>
/// Merkezi PAVO gün sonu (PerformEOD) işlemi. Raw Base64 görseller, physical dosya yolu ve file
/// metadata bu entity üzerinde tutulmaz; bunlar PosGunSonuSlipi tablosuna aittir.
/// </summary>
public class PosGunSonuIslemi : BaseEntity<int>, ITenantEntity
{
    public int KurumId { get; set; }
    public int TesisId { get; set; }
    public int PosCihaziId { get; set; }
    public PosCihazi? PosCihazi { get; set; }

    public Guid? AgentCommandId { get; set; }

    public bool UseSummary { get; set; }
    public bool Print { get; set; }

    public PosGunSonuDurumu Durum { get; set; }

    public string? GunSonuMesaji { get; set; }

    [MaxLength(64)]
    public string? BatchNo { get; set; }

    [MaxLength(64)]
    public string? EodDateTime { get; set; }

    [MaxLength(64)]
    public string? PavoErrorCode { get; set; }

    [MaxLength(1024)]
    public string? PavoMessage { get; set; }

    /// <summary>Sanitized (cardNo removed) eodData JSON. Never raw PAN/Base64.</summary>
    public string? EodDataJson { get; set; }

    public DateTime BaslatilmaTarihi { get; set; }
    public DateTime? TamamlanmaTarihi { get; set; }

    [MaxLength(128)]
    public string? RequestedBy { get; set; }

    public ICollection<PosGunSonuSlipi> Slipler { get; set; } = [];
}
