using TOD.Platform.Persistence.Rdbms.Dto;

namespace STYS.Muhasebe.MuhasebeVergiHesapEslemeleri.Dtos;

public class MuhasebeVergiHesapEslemeDto : BaseRdbmsDto<int>
{
    public int? TesisId { get; set; }
    public string VergiTipi { get; set; } = string.Empty;
    public decimal Oran { get; set; }
    public int AlisKdvHesapId { get; set; }
    public string? AlisKdvHesapKodu { get; set; }
    public string? AlisKdvHesapAdi { get; set; }
    public int SatisKdvHesapId { get; set; }
    public string? SatisKdvHesapKodu { get; set; }
    public string? SatisKdvHesapAdi { get; set; }
    public bool AktifMi { get; set; }
}

public class CreateMuhasebeVergiHesapEslemeRequest
{
    public int? TesisId { get; set; }
    public string VergiTipi { get; set; } = string.Empty;
    public decimal Oran { get; set; }
    public int AlisKdvHesapId { get; set; }
    public int SatisKdvHesapId { get; set; }
    public bool AktifMi { get; set; } = true;
}

public class UpdateMuhasebeVergiHesapEslemeRequest : CreateMuhasebeVergiHesapEslemeRequest;
