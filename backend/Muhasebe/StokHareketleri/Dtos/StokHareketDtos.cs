using TOD.Platform.Persistence.Rdbms.Dto;

namespace STYS.Muhasebe.StokHareketleri.Dtos;

public class StokHareketDto : BaseRdbmsDto<int>
{
    public int DepoId { get; set; }
    public int TasinirKartId { get; set; }
    public DateTime HareketTarihi { get; set; }
    public string HareketTipi { get; set; } = string.Empty;
    public decimal Miktar { get; set; }
    public decimal BirimFiyat { get; set; }
    public decimal Tutar { get; set; }
    public string? BelgeNo { get; set; }
    public DateTime? BelgeTarihi { get; set; }
    public string? Aciklama { get; set; }
    public int? CariKartId { get; set; }
    public string? KaynakModul { get; set; }
    public int? KaynakId { get; set; }
    public string Durum { get; set; } = string.Empty;
}

public class CreateStokHareketRequest
{
    public int DepoId { get; set; }
    public int TasinirKartId { get; set; }
    public DateTime HareketTarihi { get; set; }
    public string HareketTipi { get; set; } = string.Empty;
    public decimal Miktar { get; set; }
    public decimal BirimFiyat { get; set; }
    public string? BelgeNo { get; set; }
    public DateTime? BelgeTarihi { get; set; }
    public string? Aciklama { get; set; }
    public int? CariKartId { get; set; }
    public string? KaynakModul { get; set; }
    public int? KaynakId { get; set; }
    public string Durum { get; set; } = string.Empty;
}

public class UpdateStokHareketRequest : CreateStokHareketRequest;

public class StokBakiyeDto
{
    public int DepoId { get; set; }
    public string DepoKod { get; set; } = string.Empty;
    public string DepoAd { get; set; } = string.Empty;
    public int TasinirKartId { get; set; }
    public string StokKodu { get; set; } = string.Empty;
    public string TasinirKartAd { get; set; } = string.Empty;
    public string Birim { get; set; } = string.Empty;
    public decimal GirisMiktari { get; set; }
    public decimal CikisMiktari { get; set; }
    public decimal BakiyeMiktari { get; set; }
}

public class StokKartOzetDto
{
    public int TasinirKartId { get; set; }
    public string StokKodu { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public string Birim { get; set; } = string.Empty;
    public decimal GirisMiktari { get; set; }
    public decimal CikisMiktari { get; set; }
    public decimal BakiyeMiktari { get; set; }
}
