namespace STYS.ErisimTeshis.Dto;

public class ErisimTeshisReferansDto
{
    public List<ErisimTeshisKullaniciDto> Kullanicilar { get; set; } = [];

    public List<ErisimTeshisTesisDto> Tesisler { get; set; } = [];

    public List<ErisimTeshisModulDto> Moduller { get; set; } = [];
}
