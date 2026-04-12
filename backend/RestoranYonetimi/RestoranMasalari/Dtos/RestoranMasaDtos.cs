using System.ComponentModel.DataAnnotations;
using TOD.Platform.Persistence.Rdbms.Dto;

namespace STYS.RestoranMasalari.Dtos;

public class RestoranMasaDto : BaseRdbmsDto<int>
{
    public int RestoranId { get; set; }
    public string MasaNo { get; set; } = string.Empty;
    public int Kapasite { get; set; }
    public string Durum { get; set; } = string.Empty;
    public bool AktifMi { get; set; } = true;
}

public class CreateRestoranMasaRequest
{
    [Required]
    public int RestoranId { get; set; }

    [Required]
    public string MasaNo { get; set; } = string.Empty;

    [Range(1, 1000)]
    public int Kapasite { get; set; } = 1;

    [Required]
    public string Durum { get; set; } = string.Empty;

    public bool AktifMi { get; set; } = true;
}

public class UpdateRestoranMasaRequest
{
    [Required]
    public int RestoranId { get; set; }

    [Required]
    public string MasaNo { get; set; } = string.Empty;

    [Range(1, 1000)]
    public int Kapasite { get; set; } = 1;

    [Required]
    public string Durum { get; set; } = string.Empty;

    public bool AktifMi { get; set; } = true;
}
