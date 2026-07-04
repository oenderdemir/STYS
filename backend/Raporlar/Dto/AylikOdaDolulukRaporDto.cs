namespace STYS.Raporlar.Dto;

public class AylikOdaDolulukRaporDto
{
    public int TesisId { get; set; }

    public string? TesisAdi { get; set; }

    public int Yil { get; set; }

    public int Ay { get; set; }

    public DateTime BaslangicTarihi { get; set; }

    public DateTime BitisTarihi { get; set; }

    public List<OdaDolulukOdaDto> Odalar { get; set; } = [];

    public List<OdaDolulukGunDto> Gunler { get; set; } = [];

    public OdaDolulukOzetDto Ozet { get; set; } = new();

    public List<OdaDolulukTahsilatDto> Tahsilatlar { get; set; } = [];
}
