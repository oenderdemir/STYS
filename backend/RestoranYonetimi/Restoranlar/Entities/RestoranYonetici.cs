using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Restoranlar.Entities;

public class RestoranYonetici : BaseEntity<int>
{
    public int RestoranId { get; set; }

    public Guid UserId { get; set; }

    public Restoran? Restoran { get; set; }
}

