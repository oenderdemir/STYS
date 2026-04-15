using TOD.Platform.Persistence.Rdbms.Dto;

namespace STYS.Muhasebe.CariHareketler.Dtos;

public class CariHareketDto : BaseRdbmsDto<int>
{
    public int CariKartId { get; set; }
    public DateTime HareketTarihi { get; set; }
    public string BelgeTuru { get; set; } = string.Empty;
    public string? BelgeNo { get; set; }
    public string? Aciklama { get; set; }
    public decimal BorcTutari { get; set; }
    public decimal AlacakTutari { get; set; }
    public string ParaBirimi { get; set; } = "TRY";
    public DateTime? VadeTarihi { get; set; }
    public string Durum { get; set; } = string.Empty;
    public string? KaynakModul { get; set; }
    public int? KaynakId { get; set; }
}

public class CreateCariHareketRequest
{
    public int CariKartId { get; set; }
    public DateTime HareketTarihi { get; set; }
    public string BelgeTuru { get; set; } = string.Empty;
    public string? BelgeNo { get; set; }
    public string? Aciklama { get; set; }
    public decimal BorcTutari { get; set; }
    public decimal AlacakTutari { get; set; }
    public string ParaBirimi { get; set; } = "TRY";
    public DateTime? VadeTarihi { get; set; }
    public string Durum { get; set; } = string.Empty;
    public string? KaynakModul { get; set; }
    public int? KaynakId { get; set; }
}

public class UpdateCariHareketRequest : CreateCariHareketRequest;

public class CariEkstreDto
{
    public int CariKartId { get; set; }
    public string CariKodu { get; set; } = string.Empty;
    public string UnvanAdSoyad { get; set; } = string.Empty;
    public decimal ToplamBorc { get; set; }
    public decimal ToplamAlacak { get; set; }
    public decimal Bakiye { get; set; }
    public List<CariHareketDto> Hareketler { get; set; } = [];
}

