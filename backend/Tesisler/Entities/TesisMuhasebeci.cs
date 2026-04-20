using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Tesisler.Entities;

public class TesisMuhasebeci : BaseEntity<int>
{
    public int TesisId { get; set; }

    public Guid UserId { get; set; }

    public Tesis? Tesis { get; set; }
}
