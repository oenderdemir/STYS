using TOD.Platform.Persistence.Rdbms.Dto;

namespace STYS.EkHizmetler.Dto;

public class GlobalEkHizmetTanimiDto : BaseRdbmsDto<int>
{
    public string Ad { get; set; } = string.Empty;

    public string? Aciklama { get; set; }

    public string BirimAdi { get; set; } = "Adet";

    public string? PaketIcerikHizmetKodu { get; set; }

    public bool AktifMi { get; set; } = true;
}
