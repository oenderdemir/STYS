using System.ComponentModel.DataAnnotations;

namespace STYS.Rezervasyonlar.Dto;

public class RezervasyonErkenCikisRequestDto
{
    [Required]
    public DateTime YeniCikisTarihi { get; set; }
}
