using TOD.Platform.Persistence.Rdbms.Dto;

namespace STYS.Muhasebe.KasaBankaHesaplari.Dtos;

public class KasaBankaHesapDto : BaseRdbmsDto<int>
{
    public int? TesisId { get; set; }
    public string Tip { get; set; } = string.Empty;
    public string Kod { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public int? MuhasebeHesapPlaniId { get; set; }
    public string? AnaMuhasebeHesapKodu { get; set; }
    public int? MuhasebeHesapSiraNo { get; set; }
    public string? ParaBirimi { get; set; }
    public int ValorGunSayisi { get; set; }
    public string? KartAdi { get; set; }
    public string? KartNoMaskeli { get; set; }
    public decimal? KartLimiti { get; set; }
    public int? HesapKesimGunu { get; set; }
    public int? SonOdemeGunu { get; set; }
    public int? BagliBankaHesapId { get; set; }
    public string? MuhasebeTamKod { get; set; }
    public string? MuhasebeHesapAdi { get; set; }
    public string? BankaAdi { get; set; }
    public string? SubeAdi { get; set; }
    public string? HesapNo { get; set; }
    public string? Iban { get; set; }
    public string? MusteriNo { get; set; }
    public string? HesapTuru { get; set; }
    public string? SorumluKisi { get; set; }
    public string? Lokasyon { get; set; }
    public bool AktifMi { get; set; }
    public string? Aciklama { get; set; }
}

public class CreateKasaBankaHesapRequest
{
    public int? TesisId { get; set; }
    public string Tip { get; set; } = string.Empty;
    public string? Kod { get; set; }
    public string Ad { get; set; } = string.Empty;
    public int? MuhasebeHesapPlaniId { get; set; }
    public string? ParaBirimi { get; set; }
    public int? ValorGunSayisi { get; set; }
    public string? KartAdi { get; set; }
    public string? KartNoMaskeli { get; set; }
    public decimal? KartLimiti { get; set; }
    public int? HesapKesimGunu { get; set; }
    public int? SonOdemeGunu { get; set; }
    public int? BagliBankaHesapId { get; set; }
    public string? BankaAdi { get; set; }
    public string? SubeAdi { get; set; }
    public string? HesapNo { get; set; }
    public string? Iban { get; set; }
    public string? MusteriNo { get; set; }
    public string? HesapTuru { get; set; }
    public string? SorumluKisi { get; set; }
    public string? Lokasyon { get; set; }
    public bool AktifMi { get; set; } = true;
    public string? Aciklama { get; set; }
}

public class UpdateKasaBankaHesapRequest : CreateKasaBankaHesapRequest;

public class MuhasebeHesapSecimDto
{
    public int Id { get; set; }
    public string TamKod { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
}
