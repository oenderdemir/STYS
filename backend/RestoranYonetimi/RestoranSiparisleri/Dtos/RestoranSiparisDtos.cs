using System.ComponentModel.DataAnnotations;
using TOD.Platform.Persistence.Rdbms.Dto;

namespace STYS.RestoranSiparisleri.Dtos;

public class RestoranSiparisKalemiDto
{
    public int Id { get; set; }
    public int RestoranMenuUrunId { get; set; }
    public string UrunAdiSnapshot { get; set; } = string.Empty;
    public decimal BirimFiyat { get; set; }
    public decimal Miktar { get; set; }
    public decimal SatirToplam { get; set; }
    public string Durum { get; set; } = string.Empty;
    public string? Notlar { get; set; }
}

public class RestoranSiparisDto : BaseRdbmsDto<int>
{
    public int RestoranId { get; set; }
    public int? RestoranMasaId { get; set; }
    public string SiparisNo { get; set; } = string.Empty;
    public string SiparisDurumu { get; set; } = string.Empty;
    public decimal ToplamTutar { get; set; }
    public decimal OdenenTutar { get; set; }
    public decimal KalanTutar { get; set; }
    public string ParaBirimi { get; set; } = "TRY";
    public string OdemeDurumu { get; set; } = string.Empty;
    public string? Notlar { get; set; }
    public DateTime SiparisTarihi { get; set; }
    public List<RestoranSiparisKalemiDto> Kalemler { get; set; } = [];
}

public class CreateRestoranSiparisKalemiRequest
{
    [Required]
    public int RestoranMenuUrunId { get; set; }

    [Range(0.01, 99999)]
    public decimal Miktar { get; set; }

    public string? Notlar { get; set; }
}

public class CreateRestoranSiparisRequest
{
    [Required]
    public int RestoranId { get; set; }

    public int? RestoranMasaId { get; set; }

    [Required]
    [StringLength(3, MinimumLength = 3)]
    public string ParaBirimi { get; set; } = "TRY";

    public string? Notlar { get; set; }

    [MinLength(1)]
    public List<CreateRestoranSiparisKalemiRequest> Kalemler { get; set; } = [];
}

public class UpdateRestoranSiparisRequest
{
    public int? RestoranMasaId { get; set; }
    public string? Notlar { get; set; }
    [MinLength(1)]
    public List<CreateRestoranSiparisKalemiRequest> Kalemler { get; set; } = [];
}

public class UpdateRestoranSiparisDurumRequest
{
    [Required]
    public string SiparisDurumu { get; set; } = string.Empty;
}

public class RestoranSiparisOdemeOzetiDto
{
    public decimal SiparisToplami { get; set; }
    public decimal OdenenTutar { get; set; }
    public decimal KalanTutar { get; set; }
    public string OdemeDurumu { get; set; } = string.Empty;
    public List<STYS.RestoranOdemeleri.Dtos.RestoranOdemeDto> Odemeler { get; set; } = [];
}
