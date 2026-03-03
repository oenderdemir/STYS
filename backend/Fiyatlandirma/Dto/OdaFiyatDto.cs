using TOD.Platform.Persistence.Rdbms.Dto;

namespace STYS.Fiyatlandirma.Dto;

public class OdaFiyatDto : BaseRdbmsDto<int>
{
    public int TesisOdaTipiId { get; set; }

    public int KonaklamaTipiId { get; set; }

    public int MisafirTipiId { get; set; }

    public int KisiSayisi { get; set; } = 1;

    public decimal Fiyat { get; set; }

    public string ParaBirimi { get; set; } = "TRY";

    public DateTime BaslangicTarihi { get; set; }

    public DateTime BitisTarihi { get; set; }

    public bool AktifMi { get; set; } = true;
}
