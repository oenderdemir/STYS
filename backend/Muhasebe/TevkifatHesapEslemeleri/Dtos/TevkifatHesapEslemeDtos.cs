using TOD.Platform.Persistence.Rdbms.Dto;

namespace STYS.Muhasebe.TevkifatHesapEslemeleri.Dtos;

public class TevkifatHesapEslemeDto : BaseRdbmsDto<int>
{
    public int? TesisId { get; set; }
    public string? TesisAdi { get; set; }
    public string IslemYonu { get; set; } = string.Empty;
    public int TevkifatPay { get; set; }
    public int TevkifatPayda { get; set; }
    public int MuhasebeHesapPlaniId { get; set; }
    public string? MuhasebeHesapKodu { get; set; }
    public string? MuhasebeHesapAdi { get; set; }
    public bool AktifMi { get; set; }
    public string? Aciklama { get; set; }
}

public class CreateTevkifatHesapEslemeRequest
{
    public int? TesisId { get; set; }
    public string IslemYonu { get; set; } = string.Empty;
    public int TevkifatPay { get; set; }
    public int TevkifatPayda { get; set; } = 10;
    public int MuhasebeHesapPlaniId { get; set; }
    public bool AktifMi { get; set; } = true;
    public string? Aciklama { get; set; }
}

public class UpdateTevkifatHesapEslemeRequest : CreateTevkifatHesapEslemeRequest;
