namespace STYS.Raporlar.Dto;

public class OdaDolulukGunDto
{
    public DateTime Tarih { get; set; }

    public string GunAdi { get; set; } = "";

    public List<OdaDolulukHucreDto> Hucreler { get; set; } = [];
}
