using TOD.Platform.Persistence.Rdbms.Dto;

namespace STYS.Muhasebe.Depolar.Dtos;

public class DepoDto : BaseRdbmsDto<int>
{
    public int? TesisId { get; set; }
    public int? UstDepoId { get; set; }
    public int? MuhasebeHesapPlaniId { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public string MalzemeKayitTipi { get; set; } = "MalzemeleriAyriKayittaTut";
    public bool SatisFiyatlariniGoster { get; set; }
    public bool AvansGenel { get; set; }
    public bool AktifMi { get; set; } = true;
    public string? Aciklama { get; set; }
    public List<DepoCikisGrupDto> CikisGruplari { get; set; } = [];
}

public class CreateDepoRequest
{
    public int? TesisId { get; set; }
    public int? UstDepoId { get; set; }
    public int? MuhasebeHesapPlaniId { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public string MalzemeKayitTipi { get; set; } = "MalzemeleriAyriKayittaTut";
    public bool SatisFiyatlariniGoster { get; set; }
    public bool AvansGenel { get; set; }
    public bool AktifMi { get; set; } = true;
    public string? Aciklama { get; set; }
    public List<CreateDepoCikisGrupRequest> CikisGruplari { get; set; } = [];
}

public class UpdateDepoRequest : CreateDepoRequest;

public class DepoCikisGrupDto : BaseRdbmsDto<int>
{
    public int DepoId { get; set; }
    public string CikisGrupAdi { get; set; } = string.Empty;
    public decimal KarOrani { get; set; }
    public int? LokasyonId { get; set; }
}

public class CreateDepoCikisGrupRequest
{
    public string CikisGrupAdi { get; set; } = string.Empty;
    public decimal KarOrani { get; set; }
    public int? LokasyonId { get; set; }
}
