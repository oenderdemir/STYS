using TOD.Platform.Persistence.Rdbms.Dto;

namespace STYS.EkHizmetler.Dto;

public class EkHizmetTarifeDto : BaseRdbmsDto<int>
{
    public int TesisId { get; set; }

    public int EkHizmetId { get; set; }

    public string EkHizmetAdi { get; set; } = string.Empty;

    public string? EkHizmetAciklama { get; set; }

    public string BirimAdi { get; set; } = "Adet";

    public decimal BirimFiyat { get; set; }

    public string ParaBirimi { get; set; } = "TRY";

    public DateTime BaslangicTarihi { get; set; }

    public DateTime BitisTarihi { get; set; }

    public bool AktifMi { get; set; } = true;
}
