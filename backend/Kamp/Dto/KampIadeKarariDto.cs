namespace STYS.Kamp.Dto;

public class KampIadeKarariDto
{
    public bool IadeVarMi { get; set; }
    public decimal IadeTutari { get; set; }
    public decimal KesintiTutari { get; set; }
    public string Gerekce { get; set; } = string.Empty;
}
