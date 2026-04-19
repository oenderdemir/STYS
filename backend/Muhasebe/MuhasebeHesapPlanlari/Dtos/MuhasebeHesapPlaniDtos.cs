using TOD.Platform.Persistence.Rdbms.Dto;

namespace STYS.Muhasebe.MuhasebeHesapPlanlari.Dtos;

public class MuhasebeHesapPlaniDto : BaseRdbmsDto<int>
{
    public string Kod { get; set; } = string.Empty;
    public string TamKod { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public int SeviyeNo { get; set; }
    public int? UstHesapId { get; set; }
    public bool HasChildren { get; set; }
    public bool AktifMi { get; set; } = true;
    public string? Aciklama { get; set; }
}

public class CreateMuhasebeHesapPlaniRequest
{
    public string Kod { get; set; } = string.Empty;
    public string TamKod { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public int SeviyeNo { get; set; }
    public int? UstHesapId { get; set; }
    public bool AktifMi { get; set; } = true;
    public string? Aciklama { get; set; }
}

public class UpdateMuhasebeHesapPlaniRequest : CreateMuhasebeHesapPlaniRequest;
