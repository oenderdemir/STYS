using System.ComponentModel.DataAnnotations;
using TOD.Platform.Persistence.Rdbms.Entities;
using STYS.Entegrasyonlar.Pos.Dtos;

namespace STYS.Entegrasyonlar.Pos.Entities;

public enum PosSaglayici { Pavo = 0, Diger = 1 }

public sealed class PosCihazi : BaseEntity<int>, ITenantEntity
{
    public int KurumId { get; set; }
    public int TesisId { get; set; }
    public int? AgentId { get; set; }
    public PosSaglayici Saglayici { get; set; } = PosSaglayici.Pavo;

    [MaxLength(128)]
    public string Ad { get; set; } = string.Empty;

    [MaxLength(64)]
    public string SeriNo { get; set; } = string.Empty;

    [MaxLength(64)]
    public string? IpAdresi { get; set; }

    [MaxLength(128)]
    public string? AgentLocalDeviceId { get; set; }

    public int? HttpPort { get; set; }
    public int? HttpsPort { get; set; }

    [MaxLength(256)]
    public string? Fingerprint { get; set; }

    [MaxLength(256)]
    public string? TargetFingerprint { get; set; }

    [MaxLength(32)]
    public string? PairingCode { get; set; }

    public long? PairingId { get; set; }
    public long TransactionSequence { get; set; }
    public bool EslesmeOnayliMi { get; set; }
    public bool AktifMi { get; set; } = true;
    public DateTime? SonBaglantiTarihi { get; set; }
    public DateTime? LastHealthCheckAt { get; set; }
    public DateTime? LastHealthSuccessAt { get; set; }
    public PavoDeviceHealthStatus LastHealthStatus { get; set; } = PavoDeviceHealthStatus.Unknown;

    [MaxLength(1024)]
    public string? LastHealthError { get; set; }

    [MaxLength(1024)]
    public string? Aciklama { get; set; }

    public ICollection<PosTerminal> Terminaller { get; set; } = [];
}
