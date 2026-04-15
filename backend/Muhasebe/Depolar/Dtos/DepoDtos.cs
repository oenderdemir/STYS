using TOD.Platform.Persistence.Rdbms.Dto;

namespace STYS.Muhasebe.Depolar.Dtos;

public class DepoDto : BaseRdbmsDto<int>
{
    public int? TesisId { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public bool AktifMi { get; set; } = true;
    public string? Aciklama { get; set; }
}

public class CreateDepoRequest
{
    public int? TesisId { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public bool AktifMi { get; set; } = true;
    public string? Aciklama { get; set; }
}

public class UpdateDepoRequest : CreateDepoRequest;
