using System.ComponentModel.DataAnnotations;

namespace STYS.Kamp.Dto;

public class KampTahsisOtomatikKararRequestDto
{
    [Range(1, int.MaxValue)]
    public int KampDonemiId { get; set; }

    [Range(1, int.MaxValue)]
    public int TesisId { get; set; }
}
