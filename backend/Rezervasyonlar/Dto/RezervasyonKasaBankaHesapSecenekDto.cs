namespace STYS.Rezervasyonlar.Dto;

public class RezervasyonKasaBankaHesapSecenekDto
{
    public int Id { get; set; }

    public string Ad { get; set; } = string.Empty;

    public string Tip { get; set; } = string.Empty;

    public string Kod { get; set; } = string.Empty;

    /// <summary>Hesaba bagli aktif ve kullanima hazir fiziksel POS terminalleri. Bos ise manuel akis aynen kullanilir.</summary>
    public List<RezervasyonPosTerminalSecenekDto> PosTerminaller { get; set; } = [];
}

public class RezervasyonPosTerminalSecenekDto
{
    public int Id { get; set; }
    public string SaglayiciKodu { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
}
