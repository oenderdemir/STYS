using System.ComponentModel.DataAnnotations;

namespace STYS.Kamp.Dto;

public class KampTahsisKararRequestDto
{
    [Required]
    public string Durum { get; set; } = KampBasvuruDurumlari.Beklemede;
}
