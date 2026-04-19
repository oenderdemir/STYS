using TOD.Platform.Persistence.Rdbms.Dto;

namespace STYS.Muhasebe.Hesaplar.Dtos;

public class HesapDto : BaseRdbmsDto<int>
{
    public string Ad { get; set; } = string.Empty;
    public int MuhasebeHesapPlaniId { get; set; }
    public string? MuhasebeTamKod { get; set; }
    public string? MuhasebeHesapAdi { get; set; }
    public bool GenelHesapMi { get; set; }
    public string? MuhasebeFormu { get; set; }
    public bool AktifMi { get; set; }
    public string? Aciklama { get; set; }
    public List<int> KasaHesapIds { get; set; } = [];
    public List<int> BankaHesapIds { get; set; } = [];
    public List<int> DepoIds { get; set; } = [];
}

public class CreateHesapRequest
{
    public string Ad { get; set; } = string.Empty;
    public int MuhasebeHesapPlaniId { get; set; }
    public bool GenelHesapMi { get; set; }
    public string? MuhasebeFormu { get; set; }
    public bool AktifMi { get; set; } = true;
    public string? Aciklama { get; set; }
    public List<int>? KasaHesapIds { get; set; }
    public List<int>? BankaHesapIds { get; set; }
    public List<int>? DepoIds { get; set; }
}

public class UpdateHesapRequest : CreateHesapRequest;

public class HesapLookupDto
{
    public int Id { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
}
