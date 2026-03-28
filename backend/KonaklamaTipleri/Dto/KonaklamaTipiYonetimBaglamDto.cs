namespace STYS.KonaklamaTipleri.Dto;

public class KonaklamaTipiYonetimBaglamDto
{
    public bool GlobalTipYonetimiYapabilirMi { get; set; }

    public List<KonaklamaTipiTesisDto> Tesisler { get; set; } = [];
}
