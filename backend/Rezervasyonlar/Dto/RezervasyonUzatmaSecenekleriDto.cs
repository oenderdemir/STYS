namespace STYS.Rezervasyonlar.Dto;

public class RezervasyonUzatmaSecenekleriDto
{
    public int RezervasyonId { get; set; }

    public string ReferansNo { get; set; } = string.Empty;

    public DateTime MevcutCikisTarihi { get; set; }

    public DateTime YeniCikisTarihi { get; set; }

    /// <summary>Bkz. RezervasyonUzatmaSonucKodlari.</summary>
    public string SonucKodu { get; set; } = string.Empty;

    public string Mesaj { get; set; } = string.Empty;

    public List<RezervasyonUzatmaSecenegiDto> Secenekler { get; set; } = [];
}
