namespace STYS.Rezervasyonlar.Dto;

public class RezervasyonKasaBankaHesapSecenekDto
{
    public int Id { get; set; }

    public string Ad { get; set; } = string.Empty;

    public string Tip { get; set; } = string.Empty;

    public string Kod { get; set; } = string.Empty;

    /// <summary>Hesaba bagli aktif ve eslesmis ilk PAVO terminali. Null ise manuel akis aynen kullanilir.</summary>
    public int? PavoTerminalId { get; set; }

    public string? PavoTerminalAdi { get; set; }
}
