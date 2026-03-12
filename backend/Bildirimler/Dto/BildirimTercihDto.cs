namespace STYS.Bildirimler.Dto;

public class BildirimTercihDto
{
    public bool BildirimlerAktifMi { get; set; } = true;
    public string MinimumSeverity { get; set; } = string.Empty;
    public List<string> IzinliTipler { get; set; } = [];
    public List<string> IzinliKaynaklar { get; set; } = [];
    public List<string> MevcutTipler { get; set; } = [];
    public List<string> MevcutKaynaklar { get; set; } = [];
}
