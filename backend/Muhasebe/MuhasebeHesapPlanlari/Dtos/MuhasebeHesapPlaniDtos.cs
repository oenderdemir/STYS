using TOD.Platform.Persistence.Rdbms.Dto;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;

namespace STYS.Muhasebe.MuhasebeHesapPlanlari.Dtos;

public class MuhasebeHesapPlaniDto : BaseRdbmsDto<int>
{
    public string Kod { get; set; } = string.Empty;
    public string TamKod { get; set; } = string.Empty;
    public string? ResmiKod { get; set; }
    public string? UygulamaKodu { get; set; }
    public string Ad { get; set; } = string.Empty;
    public int SeviyeNo { get; set; }
    public HesapTipi HesapTipi { get; set; }
    public int? TesisId { get; set; }
    public int? UstHesapId { get; set; }
    public bool HasChildren { get; set; }
    public bool AktifMi { get; set; } = true;
    public bool DetayHesapMi { get; set; }
    public bool HareketGorebilirMi { get; set; } = true;
    public string? Aciklama { get; set; }
}

public class CreateMuhasebeHesapPlaniRequest
{
    public string Kod { get; set; } = string.Empty;
    public string TamKod { get; set; } = string.Empty;
    public string? ResmiKod { get; set; }
    public string? UygulamaKodu { get; set; }
    public string Ad { get; set; } = string.Empty;
    public int SeviyeNo { get; set; }
    public HesapTipi HesapTipi { get; set; }
    public int? TesisId { get; set; }
    public int? UstHesapId { get; set; }
    public bool AktifMi { get; set; } = true;
    public bool DetayHesapMi { get; set; }
    public bool HareketGorebilirMi { get; set; } = true;
    public string? Aciklama { get; set; }
}

public class UpdateMuhasebeHesapPlaniRequest : CreateMuhasebeHesapPlaniRequest;