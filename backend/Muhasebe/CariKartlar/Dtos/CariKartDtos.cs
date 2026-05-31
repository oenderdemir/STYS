using TOD.Platform.Persistence.Rdbms.Dto;

namespace STYS.Muhasebe.CariKartlar.Dtos;

public class CariKartDto : BaseRdbmsDto<int>
{
    public int? TesisId { get; set; }
    public string CariTipi { get; set; } = string.Empty;
    public string CariKodu { get; set; } = string.Empty;
    public int? MuhasebeHesapPlaniId { get; set; }
    public string? AnaMuhasebeHesapKodu { get; set; }
    public int? MuhasebeHesapSiraNo { get; set; }
    public string UnvanAdSoyad { get; set; } = string.Empty;
    public string? VergiNoTckn { get; set; }
    public string? VergiDairesi { get; set; }
    public string? Telefon { get; set; }
    public string? Eposta { get; set; }
    public string? Adres { get; set; }
    public string? Il { get; set; }
    public string? Ilce { get; set; }
    public bool AktifMi { get; set; } = true;
    public bool EFaturaMukellefiMi { get; set; }
    public bool EArsivKapsamindaMi { get; set; }
    public string? Aciklama { get; set; }
    public DateTime? AcilisBakiyeTarihi { get; set; }
    public decimal? AcilisBakiyeTutari { get; set; }
    public string? AcilisBakiyeYonu { get; set; }
    public string? BankaAdi { get; set; }
    public string? Iban { get; set; }
    public bool AcilisBakiyeDuzeltilebilirMi { get; set; }
    public List<CariKartYetkiliKisiDto> YetkiliKisiler { get; set; } = [];
}

public class CreateCariKartRequest
{
    public int? TesisId { get; set; }
    public string CariTipi { get; set; } = string.Empty;
    public string? CariKodu { get; set; }
    public string UnvanAdSoyad { get; set; } = string.Empty;
    public string? VergiNoTckn { get; set; }
    public string? VergiDairesi { get; set; }
    public string? Telefon { get; set; }
    public string? Eposta { get; set; }
    public string? Adres { get; set; }
    public string? Il { get; set; }
    public string? Ilce { get; set; }
    public bool AktifMi { get; set; } = true;
    public bool EFaturaMukellefiMi { get; set; }
    public bool EArsivKapsamindaMi { get; set; }
    public string? Aciklama { get; set; }
    public DateTime? AcilisBakiyeTarihi { get; set; }
    public decimal? AcilisBakiyeTutari { get; set; }
    public string? AcilisBakiyeYonu { get; set; }
    public string? BankaAdi { get; set; }
    public string? Iban { get; set; }
    public List<CariKartYetkiliKisiDto> YetkiliKisiler { get; set; } = [];
}

public class UpdateCariKartRequest : CreateCariKartRequest;

public class CariKartAcilisBakiyesiDuzeltRequest
{
    public decimal YeniTutar { get; set; }
    public string? YeniYonu { get; set; }
    public DateTime? DuzeltmeTarihi { get; set; }
}

public class CariKartYetkiliKisiDto : BaseRdbmsDto<int>
{
    public int? CariKartId { get; set; }
    public string AdSoyad { get; set; } = string.Empty;
    public string? GorevUnvan { get; set; }
    public string? Telefon { get; set; }
    public string? Eposta { get; set; }
    public string? Aciklama { get; set; }
}

public class CariBakiyeDto
{
    public int CariKartId { get; set; }
    public string CariKodu { get; set; } = string.Empty;
    public string UnvanAdSoyad { get; set; } = string.Empty;
    public decimal ToplamBorc { get; set; }
    public decimal ToplamAlacak { get; set; }
    public decimal Bakiye { get; set; }
    public string ParaBirimi { get; set; } = "TRY";
}

