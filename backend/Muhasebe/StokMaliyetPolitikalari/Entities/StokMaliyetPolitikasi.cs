using STYS.Tesisler.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Muhasebe.StokMaliyetPolitikalari.Entities;

public class StokMaliyetPolitikasi : BaseEntity<int>
{
    public int TesisId { get; set; }
    public int MaliYil { get; set; }
    public string MaliyetYontemi { get; set; } = string.Empty;

    public Tesis? Tesis { get; set; }
}
