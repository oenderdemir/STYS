using System.ComponentModel.DataAnnotations;
using STYS.OdaOzellikleri.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.OdaTipleri.Entities;

public class TesisOdaTipiOzellikDeger : BaseEntity<int>
{
    public int TesisOdaTipiId { get; set; }

    public int OdaOzellikId { get; set; }

    [MaxLength(512)]
    public string? Deger { get; set; }

    public OdaTipi? TesisOdaTipi { get; set; }

    public OdaOzellik? OdaOzellik { get; set; }
}
