using TOD.Platform.Persistence.Rdbms.Dto;

namespace STYS.Muhasebe.TahsilatOdemeBelgeleri.Dtos;

public class TahsilatOdemeBelgesiDto : BaseRdbmsDto<int>
{
    public string BelgeNo { get; set; } = string.Empty;
    public DateTime BelgeTarihi { get; set; }
    public string BelgeTipi { get; set; } = string.Empty;
    public int CariKartId { get; set; }
    public decimal Tutar { get; set; }
    public string ParaBirimi { get; set; } = "TRY";
    public string OdemeYontemi { get; set; } = string.Empty;
    public string? Aciklama { get; set; }
    public string? KaynakModul { get; set; }
    public int? KaynakId { get; set; }
    public string Durum { get; set; } = string.Empty;
}

public class CreateTahsilatOdemeBelgesiRequest
{
    public string BelgeNo { get; set; } = string.Empty;
    public DateTime BelgeTarihi { get; set; }
    public string BelgeTipi { get; set; } = string.Empty;
    public int CariKartId { get; set; }
    public decimal Tutar { get; set; }
    public string ParaBirimi { get; set; } = "TRY";
    public string OdemeYontemi { get; set; } = string.Empty;
    public string? Aciklama { get; set; }
    public string? KaynakModul { get; set; }
    public int? KaynakId { get; set; }
    public string Durum { get; set; } = string.Empty;
}

public class UpdateTahsilatOdemeBelgesiRequest : CreateTahsilatOdemeBelgesiRequest;

public class TahsilatOdemeOzetDto
{
    public DateTime Gun { get; set; }
    public decimal ToplamTahsilat { get; set; }
    public decimal ToplamOdeme { get; set; }
    public decimal Net { get; set; }
    public string ParaBirimi { get; set; } = "TRY";
}

