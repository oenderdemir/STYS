using TOD.Platform.Persistence.Rdbms.Dto;

namespace STYS.Muhasebe.PaketTurleri.Dtos;

public class PaketTuruDto : BaseRdbmsDto<int>
{
    public string Ad { get; set; } = string.Empty;
    public string KisaAd { get; set; } = string.Empty;
    public bool AktifMi { get; set; } = true;
}

public class CreatePaketTuruRequest
{
    public string Ad { get; set; } = string.Empty;
    public string KisaAd { get; set; } = string.Empty;
    public bool AktifMi { get; set; } = true;
}

public class UpdatePaketTuruRequest : CreatePaketTuruRequest;
