using System.ComponentModel.DataAnnotations;
using STYS.Fiyatlandirma;
using STYS.KonaklamaTipleri.Entities;
using STYS.MisafirTipleri.Entities;
using STYS.OdaTipleri.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Fiyatlandirma.Entities;

public class OdaFiyat : BaseEntity<int>
{
    public int TesisOdaTipiId { get; set; }

    public int KonaklamaTipiId { get; set; }

    public int MisafirTipiId { get; set; }

    public int KisiSayisi { get; set; } = 1;

    [Required]
    [MaxLength(32)]
    public string KullanimSekli { get; set; } = OdaFiyatKullanimSekilleri.KisiBasi;

    public decimal Fiyat { get; set; }

    [Required]
    [MaxLength(3)]
    public string ParaBirimi { get; set; } = "TRY";

    public DateTime BaslangicTarihi { get; set; }

    public DateTime BitisTarihi { get; set; }

    public bool AktifMi { get; set; } = true;

    public OdaTipi? TesisOdaTipi { get; set; }

    public KonaklamaTipi? KonaklamaTipi { get; set; }

    public MisafirTipi? MisafirTipi { get; set; }
}
