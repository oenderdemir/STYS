using System.ComponentModel.DataAnnotations;
using TOD.Platform.Persistence.Rdbms.Dto;

namespace STYS.RestoranMenuUrunleri.Dtos;

public class RestoranMenuUrunDto : BaseRdbmsDto<int>
{
    public int RestoranMenuKategoriId { get; set; }
    public string Ad { get; set; } = string.Empty;
    public string? Aciklama { get; set; }
    public decimal Fiyat { get; set; }
    public string ParaBirimi { get; set; } = "TRY";
    public int HazirlamaSuresiDakika { get; set; }
    public bool AktifMi { get; set; } = true;
}

public class CreateRestoranMenuUrunRequest
{
    [Required]
    public int RestoranMenuKategoriId { get; set; }

    [Required]
    public string Ad { get; set; } = string.Empty;

    public string? Aciklama { get; set; }

    [Range(0, 9999999)]
    public decimal Fiyat { get; set; }

    [Required]
    [StringLength(3, MinimumLength = 3)]
    public string ParaBirimi { get; set; } = "TRY";

    [Range(0, 1000)]
    public int HazirlamaSuresiDakika { get; set; }

    public bool AktifMi { get; set; } = true;
}

public class UpdateRestoranMenuUrunRequest
{
    [Required]
    public int RestoranMenuKategoriId { get; set; }

    [Required]
    public string Ad { get; set; } = string.Empty;

    public string? Aciklama { get; set; }

    [Range(0, 9999999)]
    public decimal Fiyat { get; set; }

    [Required]
    [StringLength(3, MinimumLength = 3)]
    public string ParaBirimi { get; set; } = "TRY";

    [Range(0, 1000)]
    public int HazirlamaSuresiDakika { get; set; }

    public bool AktifMi { get; set; } = true;
}
