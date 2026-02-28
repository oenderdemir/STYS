using System.ComponentModel.DataAnnotations;
using TOD.Platform.Persistence.Rdbms.Dto;

namespace STYS.OdaTipleri.Dto;

public class OdaTipiDto : BaseRdbmsDto<int>
{
    [Required]
    public int TesisId { get; set; }

    [Required]
    public int OdaSinifiId { get; set; }

    [Required]
    public string Ad { get; set; } = string.Empty;

    public bool PaylasimliMi { get; set; }

    [Range(1, int.MaxValue)]
    public int Kapasite { get; set; } = 1;

    public bool BalkonVarMi { get; set; }

    public bool KlimaVarMi { get; set; }

    public decimal? Metrekare { get; set; }

    public bool AktifMi { get; set; } = true;
}
