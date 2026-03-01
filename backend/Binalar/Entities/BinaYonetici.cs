using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Binalar.Entities;

public class BinaYonetici : BaseEntity<int>
{
    public int BinaId { get; set; }

    public Guid UserId { get; set; }

    public Bina? Bina { get; set; }
}
