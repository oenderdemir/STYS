using TOD.Platform.Persistence.Rdbms.Dto;

namespace STYS.Muhasebe.KonaklamaVergisiHesapEslemeleri.Dtos;

public class KonaklamaVergisiHesapEslemeDto : BaseRdbmsDto<int>
{
    public int? TesisId { get; set; }
    public string? TesisAdi { get; set; }
    public int VergiHesapId { get; set; }
    public string? VergiHesapKodu { get; set; }
    public string? VergiHesapAdi { get; set; }
    public bool AktifMi { get; set; }
    public string? Aciklama { get; set; }
}

public class CreateKonaklamaVergisiHesapEslemeRequest
{
    public int? TesisId { get; set; }
    public int VergiHesapId { get; set; }
    public bool AktifMi { get; set; } = true;
    public string? Aciklama { get; set; }
}

public class UpdateKonaklamaVergisiHesapEslemeRequest : CreateKonaklamaVergisiHesapEslemeRequest;

public class KonaklamaVergisiHesapSecenekDto
{
    public int Id { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
}
