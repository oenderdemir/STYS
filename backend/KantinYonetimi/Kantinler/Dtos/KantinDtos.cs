using System.ComponentModel.DataAnnotations;
using TOD.Platform.Persistence.Rdbms.Dto;

namespace STYS.KantinYonetimi.Kantinler.Dtos;

public class KantinDto : BaseRdbmsDto<int>
{
    public int TesisId { get; set; }
    public int DepoId { get; set; }
    public int? VarsayilanNakitKasaId { get; set; }
    public int? VarsayilanPosHesapId { get; set; }
    public int? PerakendeCariKartId { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public bool AktifMi { get; set; } = true;
    public string? Aciklama { get; set; }
    public string? DepoKod { get; set; }
    public string? DepoAd { get; set; }
    public string? VarsayilanNakitKasaAd { get; set; }
    public string? VarsayilanPosHesapAd { get; set; }
    public string? PerakendeCariKartAd { get; set; }
}

public class KantinUrunDto : BaseRdbmsDto<int>
{
    public int KantinId { get; set; }
    public int TasinirKartId { get; set; }
    public int? SiraNo { get; set; }
    public string? Barkod { get; set; }
    public decimal SatisFiyati { get; set; }
    public bool AktifMi { get; set; } = true;
    public string? Aciklama { get; set; }
    public string? StokKodu { get; set; }
    public string? UrunAdi { get; set; }
    public string? Birim { get; set; }
    public decimal KdvOrani { get; set; }
    public decimal MevcutStok { get; set; }
    public string? TakipTipi { get; set; }
}

public class KantinDepoSecenekDto
{
    public int Id { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
}

public class KantinKasaSecenekDto
{
    public int Id { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
}

public class KantinCariKartSecenekDto
{
    public int Id { get; set; }
    public string CariKodu { get; set; } = string.Empty;
    public string UnvanAdSoyad { get; set; } = string.Empty;
}

public class KantinOdemeHesapSecenekDto
{
    public int Id { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public string Tip { get; set; } = string.Empty;
}

public class KantinTasinirKartSecenekDto
{
    public int Id { get; set; }
    public string StokKodu { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public string Birim { get; set; } = string.Empty;
    public decimal KdvOrani { get; set; }
}

public class CreateKantinRequest
{
    [Required]
    public int TesisId { get; set; }

    [Required]
    public int DepoId { get; set; }

    public int? VarsayilanNakitKasaId { get; set; }
    public int? VarsayilanPosHesapId { get; set; }
    public int? PerakendeCariKartId { get; set; }

    [Required]
    [StringLength(64)]
    public string Kod { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Ad { get; set; } = string.Empty;

    public bool AktifMi { get; set; } = true;

    [StringLength(1024)]
    public string? Aciklama { get; set; }
}

public class UpdateKantinRequest : CreateKantinRequest
{
}

public class CreateKantinUrunRequest
{
    [Required]
    public int TasinirKartId { get; set; }

    [StringLength(128)]
    public string? Barkod { get; set; }

    [Range(0, 999999999)]
    public decimal SatisFiyati { get; set; }

    public bool AktifMi { get; set; } = true;

    public int? SiraNo { get; set; }

    [StringLength(1024)]
    public string? Aciklama { get; set; }
}

public class UpdateKantinUrunRequest : CreateKantinUrunRequest
{
}
