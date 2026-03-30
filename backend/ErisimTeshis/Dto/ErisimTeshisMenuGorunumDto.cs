namespace STYS.ErisimTeshis.Dto;

public class ErisimTeshisMenuGorunumDto
{
    public bool MenuKaydiBulundu { get; set; }

    public string MenuYolu { get; set; } = string.Empty;

    public string Route { get; set; } = string.Empty;

    public bool SidebardaGorunur { get; set; }

    public bool MenuYetkisiVar { get; set; }

    public List<string> GerekliMenuYetkileri { get; set; } = [];

    public List<ErisimTeshisMenuSeviyeDto> MenuZinciri { get; set; } = [];

    public string Aciklama { get; set; } = string.Empty;
}
