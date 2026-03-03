namespace STYS.Fiyatlandirma.Dto;

public class UygulananIndirimDto
{
    public int IndirimKuraliId { get; set; }

    public string KuralAdi { get; set; } = string.Empty;

    public decimal IndirimTutari { get; set; }

    public decimal SonrasiTutar { get; set; }
}
