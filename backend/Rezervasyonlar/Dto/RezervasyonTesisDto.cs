namespace STYS.Rezervasyonlar.Dto;

public class RezervasyonTesisDto
{
    public int Id { get; set; }

    public string Ad { get; set; } = string.Empty;

    public TimeSpan GirisSaati { get; set; }

    public TimeSpan CikisSaati { get; set; }
}
