using TOD.Platform.Persistence.Rdbms.Dto;

namespace STYS.Muhasebe.BankaHareketleri.Dtos;

public class BankaHareketDto : BaseRdbmsDto<int>
{
    public string BankaAdi { get; set; } = string.Empty;
    public string HesapKoduIban { get; set; } = string.Empty;
    public DateTime HareketTarihi { get; set; }
    public string HareketTipi { get; set; } = string.Empty;
    public decimal Tutar { get; set; }
    public string ParaBirimi { get; set; } = "TRY";
    public string? Aciklama { get; set; }
    public string? BelgeNo { get; set; }
    public int? CariKartId { get; set; }
    public string? KaynakModul { get; set; }
    public int? KaynakId { get; set; }
    public string Durum { get; set; } = string.Empty;
}

public class CreateBankaHareketRequest
{
    public string BankaAdi { get; set; } = string.Empty;
    public string HesapKoduIban { get; set; } = string.Empty;
    public DateTime HareketTarihi { get; set; }
    public string HareketTipi { get; set; } = string.Empty;
    public decimal Tutar { get; set; }
    public string ParaBirimi { get; set; } = "TRY";
    public string? Aciklama { get; set; }
    public string? BelgeNo { get; set; }
    public int? CariKartId { get; set; }
    public string? KaynakModul { get; set; }
    public int? KaynakId { get; set; }
    public string Durum { get; set; } = string.Empty;
}

public class UpdateBankaHareketRequest : CreateBankaHareketRequest;

