using TOD.Platform.Persistence.Rdbms.Dto;

namespace STYS.Muhasebe.TasinirKodlari.Dtos;

public class TasinirKodDto : BaseRdbmsDto<int>
{
    public string TamKod { get; set; } = string.Empty;
    public string Kod { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public int DuzeyNo { get; set; }
    public int? UstKodId { get; set; }
    public bool HasChildren { get; set; }
    public bool AktifMi { get; set; } = true;
    public string? Aciklama { get; set; }
}

public class CreateTasinirKodRequest
{
    public string TamKod { get; set; } = string.Empty;
    public string Kod { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public int DuzeyNo { get; set; }
    public int? UstKodId { get; set; }
    public bool AktifMi { get; set; } = true;
    public string? Aciklama { get; set; }
}

public class UpdateTasinirKodRequest : CreateTasinirKodRequest;

public class ImportTasinirKodSatiriRequest
{
    public string TamKod { get; set; } = string.Empty;
    public string? Kod { get; set; }
    public string Ad { get; set; } = string.Empty;
    public int DuzeyNo { get; set; }
    public string? UstTamKod { get; set; }
    public bool AktifMi { get; set; } = true;
    public string? Aciklama { get; set; }
}

public class ImportTasinirKodlariRequest
{
    public bool MevcutlariGuncelle { get; set; } = true;
    public bool PasiflestirilmeyenleriPasifYap { get; set; }
    public List<ImportTasinirKodSatiriRequest> Satirlar { get; set; } = [];
}

public class TasinirKodImportSonucDto
{
    public int Eklenen { get; set; }
    public int Guncellenen { get; set; }
    public int PasifYapilan { get; set; }
    public int ToplamIslenen { get; set; }
}
