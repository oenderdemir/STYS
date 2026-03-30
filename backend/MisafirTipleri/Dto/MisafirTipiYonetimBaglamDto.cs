namespace STYS.MisafirTipleri.Dto;

public class MisafirTipiYonetimBaglamDto
{
    public bool GlobalTipYonetimiYapabilirMi { get; set; }

    public List<MisafirTipiTesisDto> Tesisler { get; set; } = [];
}
