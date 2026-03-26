namespace STYS.ErisimTeshis.Dto;

public class ErisimTeshisScopeDto
{
    public bool AdminMi { get; set; }

    public bool ScopedMi { get; set; }

    public List<ErisimTeshisTesisDto> Tesisler { get; set; } = [];

    public List<int> BinaIdleri { get; set; } = [];

    public string Ozet { get; set; } = string.Empty;
}
