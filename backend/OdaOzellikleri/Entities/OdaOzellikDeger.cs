using System.ComponentModel.DataAnnotations;
using STYS.Odalar.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.OdaOzellikleri.Entities;

public class OdaOzellikDeger : BaseEntity<int>
{
    public int OdaId { get; set; }

    public int OdaOzellikId { get; set; }

    [MaxLength(512)]
    public string? Deger { get; set; }

    public Oda? Oda { get; set; }

    public OdaOzellik? OdaOzellik { get; set; }
}
