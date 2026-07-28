using System.ComponentModel.DataAnnotations;

namespace STYS.Rezervasyonlar.Dto;

public class RezervasyonUzatmaSecenekleriRequestDto
{
    [Required]
    public DateTime YeniCikisTarihi { get; set; }
}
