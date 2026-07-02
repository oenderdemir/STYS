namespace STYS.Raporlar.Dto;

public class OdaDolulukOdaDto
{
    public int OdaId { get; set; }

    public string OdaNo { get; set; } = "";

    public string? BinaAdi { get; set; }

    public string? OdaTipiAdi { get; set; }

    public int Kapasite { get; set; }
}
