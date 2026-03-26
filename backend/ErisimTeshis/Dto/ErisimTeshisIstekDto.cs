namespace STYS.ErisimTeshis.Dto;

public class ErisimTeshisIstekDto
{
    public Guid KullaniciId { get; set; }

    public string ModulAnahtari { get; set; } = string.Empty;

    public int? TesisId { get; set; }
}
