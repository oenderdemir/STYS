namespace STYS.ErisimTeshis.Dto;

public class ErisimTeshisMenuSeviyeDto
{
    public string Etiket { get; set; } = string.Empty;

    public string Route { get; set; } = string.Empty;

    public List<string> GerekliYetkiler { get; set; } = [];

    public bool Gorunur { get; set; }
}
