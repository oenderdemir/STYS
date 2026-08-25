using System.ComponentModel.DataAnnotations;
using TOD.Platform.Persistence.Rdbms.Dto;

namespace STYS.KantinYonetimi.KantinSatislari.Dtos;

public class KantinSatisDto : BaseRdbmsDto<int>
{
    public int TesisId { get; set; }
    public int KantinId { get; set; }
    public int SatisNoktasiId { get; set; }
    public DateTime SatisTarihi { get; set; }
    public string Durum { get; set; } = string.Empty;
    public decimal ToplamTutar { get; set; }
    public decimal MatrahToplami { get; set; }
    public decimal KdvToplami { get; set; }
    public string? Aciklama { get; set; }
    public DateTime? KesinlesmeTarihi { get; set; }
    public int? MuhasebeFisId { get; set; }
    public string? MuhasebeFisNo { get; set; }
    public string? MuhasebeFisDurumu { get; set; }
    public DateTime? MuhasebeFisOlusturmaTarihi { get; set; }
    public DateTime? IptalTarihi { get; set; }
    public string? IptalAciklamasi { get; set; }
    public string? IptalEdenKullaniciId { get; set; }
    public string? KantinKod { get; set; }
    public string? KantinAd { get; set; }
    public string? SatisNoktasiKod { get; set; }
    public string? SatisNoktasiAd { get; set; }
    public string? OdemeOzeti { get; set; }
    public List<KantinSatisSatirDto> Satirlar { get; set; } = [];
    public List<KantinSatisOdemeDto> Odemeler { get; set; } = [];
}

public class KantinSatisSatirDto : BaseRdbmsDto<int>
{
    public int KantinSatisId { get; set; }
    public int KantinUrunId { get; set; }
    public int TasinirKartId { get; set; }
    public decimal Miktar { get; set; }
    public decimal BirimSatisFiyati { get; set; }
    public decimal KdvOrani { get; set; }
    public decimal Matrah { get; set; }
    public decimal KdvTutari { get; set; }
    public decimal ToplamTutar { get; set; }
    public int? StokLotId { get; set; }
    public int? StokSeriId { get; set; }
    public int? StokHareketId { get; set; }
    public string? Barkod { get; set; }
    public string StokKodu { get; set; } = string.Empty;
    public string UrunAdi { get; set; } = string.Empty;
    public string Birim { get; set; } = string.Empty;
    public string? TakipTipi { get; set; }
    public string? LotNo { get; set; }
    public DateTime? SonKullanmaTarihi { get; set; }
    public string? SeriNo { get; set; }
}

public class KantinSatisOdemeDto : BaseRdbmsDto<int>
{
    public int KantinSatisId { get; set; }
    public string OdemeYontemi { get; set; } = string.Empty;
    public int? KasaBankaHesapId { get; set; }
    public int? TahsilatOdemeBelgesiId { get; set; }
    public decimal Tutar { get; set; }
    public string? HesapKodSnapshot { get; set; }
    public string? HesapAdSnapshot { get; set; }
    public string? TahsilatBelgeNo { get; set; }
    public DateTime? PosBeklenenValorTarihi { get; set; }
    public string? PosValorDurumu { get; set; }
}

public class CreateKantinSatisRequest
{
    [Required]
    public int KantinId { get; set; }

    [Required]
    public int SatisNoktasiId { get; set; }

    public DateTime? SatisTarihi { get; set; }

    [StringLength(1024)]
    public string? Aciklama { get; set; }
}

public class UpdateKantinSatisRequest
{
    public DateTime? SatisTarihi { get; set; }

    [StringLength(1024)]
    public string? Aciklama { get; set; }
}

public class CancelKantinSatisRequest
{
    [Required]
    [StringLength(1024)]
    public string Aciklama { get; set; } = string.Empty;
}

public class AddKantinSatisSatirRequest
{
    [Required]
    public int KantinUrunId { get; set; }

    [Range(0.000001, 999999999)]
    public decimal Miktar { get; set; }

    public int? StokLotId { get; set; }
    public int? StokSeriId { get; set; }
}

public class UpdateKantinSatisSatirRequest : AddKantinSatisSatirRequest
{
}

public class AddKantinSatisOdemeRequest
{
    [Required]
    [StringLength(32)]
    public string OdemeYontemi { get; set; } = string.Empty;

    public int? KasaBankaHesapId { get; set; }

    [Range(0.01, 999999999)]
    public decimal Tutar { get; set; }
}

public class UpdateKantinSatisOdemeRequest : AddKantinSatisOdemeRequest
{
}

public class KantinSatisBarkodUrunDto
{
    public int KantinUrunId { get; set; }
    public int TasinirKartId { get; set; }
    public string StokKodu { get; set; } = string.Empty;
    public string UrunAdi { get; set; } = string.Empty;
    public string Birim { get; set; } = string.Empty;
    public string? Barkod { get; set; }
    public decimal SatisFiyati { get; set; }
    public decimal KdvOrani { get; set; }
    public decimal MevcutStok { get; set; }
    public string TakipTipi { get; set; } = string.Empty;
}
