using STYS.Tesisler.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Kullanicilar.Entities;

public class KullaniciTesisSahiplik : BaseEntity<int>
{
    public Guid UserId { get; set; }

    public int? TesisId { get; set; }

    public Tesis? Tesis { get; set; }
}
