namespace STYS.Bildirimler.Dto;

public class BildirimTercihGuncelleRequestDto
{
    public bool BildirimlerAktifMi { get; set; } = true;
    public string MinimumSeverity { get; set; } = string.Empty;
    public List<string> IzinliTipler { get; set; } = [];
    public List<string> IzinliKaynaklar { get; set; } = [];
}
