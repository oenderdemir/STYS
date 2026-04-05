using System.ComponentModel.DataAnnotations;
using STYS.Tesisler.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Kamp.Entities;

public class KampDonemiTesis : BaseEntity<int>
{
    public int KampDonemiId { get; set; }

    public int TesisId { get; set; }

    public bool AktifMi { get; set; } = true;

    public bool BasvuruyaAcikMi { get; set; } = true;

    public int ToplamKontenjan { get; set; }

    [MaxLength(512)]
    public string? Aciklama { get; set; }

    public KampDonemi? KampDonemi { get; set; }

    public Tesis? Tesis { get; set; }
}
