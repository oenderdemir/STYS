using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Muhasebe.MuhasebeFisleri.Entities;

/// <summary>
/// Tesis ve mali yıl bazlı yevmiye no sayacı.
/// Her tesis için her mali yılda 1'den başlayan sıralı yevmiye numarası üretir.
/// </summary>
public class MuhasebeYevmiyeNoSayac : BaseEntity<int>
{
    public int TesisId { get; set; }
    public int MaliYil { get; set; }
    public int SonNumara { get; set; }
}
