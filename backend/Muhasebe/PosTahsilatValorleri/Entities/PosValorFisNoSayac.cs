using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Muhasebe.PosTahsilatValorleri.Entities;

/// <summary>
/// Tesis ve mali yil bazli POS valor transfer fisi sira no sayaci. MuhasebeYevmiyeNoSayac ile
/// ayni desen (WITH (UPDLOCK, ROWLOCK, HOLDLOCK) + retry) - Max(FisNo)+1 yaklasimi yerine
/// eszamanliliga guvenli bir sayac kullanir.
/// </summary>
public class PosValorFisNoSayac : BaseEntity<int>
{
    public int TesisId { get; set; }
    public int MaliYil { get; set; }
    public int SonNumara { get; set; }
}
