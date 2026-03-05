using System.ComponentModel.DataAnnotations;
using STYS.Odalar.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Rezervasyonlar.Entities;

public class RezervasyonSegmentOdaAtama : BaseEntity<int>
{
    public int RezervasyonSegmentId { get; set; }

    public int OdaId { get; set; }

    public int AyrilanKisiSayisi { get; set; }

    [MaxLength(64)]
    public string OdaNoSnapshot { get; set; } = string.Empty;

    [MaxLength(200)]
    public string BinaAdiSnapshot { get; set; } = string.Empty;

    [MaxLength(128)]
    public string OdaTipiAdiSnapshot { get; set; } = string.Empty;

    public bool PaylasimliMiSnapshot { get; set; }

    public int KapasiteSnapshot { get; set; }

    public RezervasyonSegment? RezervasyonSegment { get; set; }

    public Oda? Oda { get; set; }
}
