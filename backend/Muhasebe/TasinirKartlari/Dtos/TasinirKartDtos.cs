using TOD.Platform.Persistence.Rdbms.Dto;

namespace STYS.Muhasebe.TasinirKartlari.Dtos;

public class TasinirKartDto : BaseRdbmsDto<int>
{
    public int TasinirKodId { get; set; }
    public string StokKodu { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public string Birim { get; set; } = "Adet";
    public string MalzemeTipi { get; set; } = "Diger";
    public bool SarfMi { get; set; }
    public bool DemirbasMi { get; set; }
    public bool TakipliMi { get; set; }
    public decimal KdvOrani { get; set; }
    public bool AktifMi { get; set; } = true;
    public string? Aciklama { get; set; }
}

public class CreateTasinirKartRequest
{
    public int TasinirKodId { get; set; }
    public string StokKodu { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public string Birim { get; set; } = "Adet";
    public string MalzemeTipi { get; set; } = "Diger";
    public bool SarfMi { get; set; }
    public bool DemirbasMi { get; set; }
    public bool TakipliMi { get; set; }
    public decimal KdvOrani { get; set; }
    public bool AktifMi { get; set; } = true;
    public string? Aciklama { get; set; }
}

public class UpdateTasinirKartRequest : CreateTasinirKartRequest;
