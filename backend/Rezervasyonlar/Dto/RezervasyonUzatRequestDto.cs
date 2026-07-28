using System.ComponentModel.DataAnnotations;

namespace STYS.Rezervasyonlar.Dto;

public class RezervasyonUzatRequestDto
{
    [Required]
    public DateTime YeniCikisTarihi { get; set; }

    /// <summary>GetUzatmaSecenekleriAsync tarafindan uretilen kararli plan kodu (bkz.
    /// CreateUzatmaPlanKodu). Sunucu bu kodu DOGRUDAN GUVENMEZ - kaydetme sirasinda uzatma
    /// secenekleri yeniden hesaplanip bu kodla eslestirilir.</summary>
    [Required]
    public string SenaryoKodu { get; set; } = string.Empty;
}
