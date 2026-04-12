using System.ComponentModel.DataAnnotations;
using TOD.Platform.Persistence.Rdbms.Dto;

namespace STYS.RestoranOdemeleri.Dtos;

public class RestoranOdemeDto : BaseRdbmsDto<int>
{
    public int RestoranSiparisId { get; set; }
    public string OdemeTipi { get; set; } = string.Empty;
    public decimal Tutar { get; set; }
    public string ParaBirimi { get; set; } = "TRY";
    public DateTime OdemeTarihi { get; set; }
    public string? Aciklama { get; set; }
    public int? RezervasyonId { get; set; }
    public int? RezervasyonOdemeId { get; set; }
    public string Durum { get; set; } = string.Empty;
    public string? IslemReferansNo { get; set; }
}

public class CreateNakitOdemeRequest
{
    [Range(0.01, 9999999)]
    public decimal Tutar { get; set; }
    public string? Aciklama { get; set; }
}

public class CreateKrediKartiOdemeRequest
{
    [Range(0.01, 9999999)]
    public decimal Tutar { get; set; }
    public string? Aciklama { get; set; }
}

public class CreateOdayaEkleOdemeRequest
{
    [Required]
    public int RezervasyonId { get; set; }

    [Range(0.01, 9999999)]
    public decimal Tutar { get; set; }

    public string? Aciklama { get; set; }
}

public class AktifRezervasyonAramaDto
{
    public int RezervasyonId { get; set; }
    public int TesisId { get; set; }
    public string ReferansNo { get; set; } = string.Empty;
    public string MisafirAdiSoyadi { get; set; } = string.Empty;
    public string OdaNo { get; set; } = string.Empty;
    public DateTime GirisTarihi { get; set; }
    public DateTime CikisTarihi { get; set; }
}
