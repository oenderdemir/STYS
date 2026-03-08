using System.ComponentModel.DataAnnotations;
using TOD.Platform.Persistence.Rdbms.Dto;

namespace STYS.OdaKullanimBloklari.Dto;

public class OdaKullanimBlokDto : BaseRdbmsDto<int>
{
    [Required]
    public int TesisId { get; set; }

    [Required]
    public int OdaId { get; set; }

    [Required]
    public string BlokTipi { get; set; } = string.Empty;

    [Required]
    public DateTime BaslangicTarihi { get; set; }

    [Required]
    public DateTime BitisTarihi { get; set; }

    public string? Aciklama { get; set; }

    public bool AktifMi { get; set; } = true;
}

