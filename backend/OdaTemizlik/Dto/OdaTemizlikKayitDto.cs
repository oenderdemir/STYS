namespace STYS.OdaTemizlik.Dto;

public class OdaTemizlikKayitDto
{
    public int OdaId { get; set; }

    public int TesisId { get; set; }

    public string TesisAdi { get; set; } = string.Empty;

    public int BinaId { get; set; }

    public string BinaAdi { get; set; } = string.Empty;

    public string OdaNo { get; set; } = string.Empty;

    public int OdaTipiId { get; set; }

    public string OdaTipiAdi { get; set; } = string.Empty;

    public string TemizlikDurumu { get; set; } = string.Empty;
}
