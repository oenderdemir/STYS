using TOD.Platform.Persistence.Rdbms.Dto;

namespace STYS.Muhasebe.TasinirKodMuhasebeHesapEslemeleri.Dtos;

public class TasinirKodMuhasebeHesapEslemeDto : BaseRdbmsDto<int>
{
    public int TasinirKodId { get; set; }
    public string? TasinirKodKod { get; set; }
    public string? TasinirKodAd { get; set; }
    public int MuhasebeHesapPlaniId { get; set; }
    public string? MuhasebeHesapKod { get; set; }
    public string? MuhasebeHesapAd { get; set; }
    public string IslemTuru { get; set; } = string.Empty;
    public string MalzemeTipi { get; set; } = string.Empty;
    public string HareketTipi { get; set; } = string.Empty;
    public bool AktifMi { get; set; }
    public bool VarsayilanMi { get; set; }
}

public class CreateTasinirKodMuhasebeHesapEslemeRequest
{
    public int TasinirKodId { get; set; }
    public int MuhasebeHesapPlaniId { get; set; }
    public string IslemTuru { get; set; } = "Alis";
    public string MalzemeTipi { get; set; } = string.Empty;
    public string HareketTipi { get; set; } = string.Empty;
    public bool AktifMi { get; set; } = true;
    public bool VarsayilanMi { get; set; }
}

public class UpdateTasinirKodMuhasebeHesapEslemeRequest : CreateTasinirKodMuhasebeHesapEslemeRequest;

public class TasinirKodMuhasebeHesapEslemeFilterDto
{
    public int? TasinirKodId { get; set; }
    public string? IslemTuru { get; set; }
    public string? MalzemeTipi { get; set; }
    public string? HareketTipi { get; set; }
}