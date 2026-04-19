using STYS.Muhasebe.Depolar.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Muhasebe.Hesaplar.Entities;

public class HesapDepoBaglanti : BaseEntity<int>
{
    public int HesapId { get; set; }
    public int DepoId { get; set; }

    public Hesap? Hesap { get; set; }
    public Depo? Depo { get; set; }
}
