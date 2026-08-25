using System.ComponentModel.DataAnnotations;
using TOD.Platform.Persistence.Rdbms.Dto;

namespace STYS.KantinYonetimi.KantinSatislari.Dtos;

public class KantinSatisIadeSatirDto : BaseRdbmsDto<int>
{
    public int KantinSatisIadeId { get; set; }
    public int KantinSatisSatirId { get; set; }
    public decimal Miktar { get; set; }

    public int TasinirKartId { get; set; }
    public string StokKodu { get; set; } = string.Empty;
    public string UrunAdi { get; set; } = string.Empty;
    public string Birim { get; set; } = string.Empty;
    public string TakipTipi { get; set; } = string.Empty;
    public string? LotNo { get; set; }
    public string? SeriNo { get; set; }
    public decimal BirimSatisFiyati { get; set; }
    public decimal KdvOrani { get; set; }
    public decimal? MaliyetBirimFiyat { get; set; }
    public decimal? MaliyetTutari { get; set; }
    public int? StokHareketId { get; set; }

    public decimal SatilanMiktar { get; set; }
    public decimal OncekiIadeMiktari { get; set; }
    public decimal KalanMiktar { get; set; }
}

public class KantinSatisIadeDto : BaseRdbmsDto<int>
{
    public int TesisId { get; set; }
    public int KantinSatisId { get; set; }
    public DateTime IadeTarihi { get; set; }
    public string Durum { get; set; } = string.Empty;
    public string? Aciklama { get; set; }
    public string? OlusturanKullaniciId { get; set; }
    public DateTime? KesinlesmeTarihi { get; set; }
    public string FinansalIadeDurumu { get; set; } = string.Empty;
    public List<KantinSatisIadeSatirDto> Satirlar { get; set; } = [];
}

public class CreateKantinSatisIadeSatirRequest
{
    [Required]
    public int KantinSatisSatirId { get; set; }

    public decimal Miktar { get; set; }
}

public class CreateKantinSatisIadeRequest
{
    [Required]
    public int KantinSatisId { get; set; }

    [StringLength(1024)]
    public string? Aciklama { get; set; }

    [Required]
    [MinLength(1)]
    public List<CreateKantinSatisIadeSatirRequest> Satirlar { get; set; } = [];
}

public class KantinSatisIadeOzetDto
{
    public int KantinSatisSatirId { get; set; }
    public decimal SatilanMiktar { get; set; }
    public decimal OncekiIadeMiktari { get; set; }
    public decimal KalanMiktar { get; set; }
}
