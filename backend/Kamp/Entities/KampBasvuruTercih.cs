using System.ComponentModel.DataAnnotations;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Kamp.Entities;

public class KampBasvuruTercih : BaseEntity<int>
{
    public int KampBasvuruId { get; set; }

    public int TercihSirasi { get; set; }

    public int KampDonemiId { get; set; }

    public int TesisId { get; set; }

    [Required]
    [MaxLength(32)]
    public string KonaklamaBirimiTipi { get; set; } = string.Empty;

    public KampBasvuru? KampBasvuru { get; set; }

    public KampDonemi? KampDonemi { get; set; }

    public Tesisler.Entities.Tesis? Tesis { get; set; }
}
