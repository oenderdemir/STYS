using STYS.Tesisler.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Muhasebe.MuhasebeDonemleri.Entities;

/// <summary>
/// Muhasebe dönemi. Her tesis için mali yıl ve dönem bazında tanımlanır.
/// </summary>
public class MuhasebeDonem : BaseEntity<int>
{
    public int TesisId { get; set; }
    public Tesis? Tesis { get; set; }

    public int MaliYil { get; set; }
    public int DonemNo { get; set; }

    public DateTime BaslangicTarihi { get; set; }
    public DateTime BitisTarihi { get; set; }

    public bool KapaliMi { get; set; }
    public DateTime? KapanisTarihi { get; set; }

    public string? Aciklama { get; set; }
}
