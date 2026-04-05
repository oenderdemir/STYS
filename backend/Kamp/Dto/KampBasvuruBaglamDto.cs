namespace STYS.Kamp.Dto;

public class KampBasvuruBaglamDto
{
    public List<KampBasvuruDonemSecenekDto> Donemler { get; set; } = [];
}

public class KampBasvuruDonemSecenekDto
{
    public int Id { get; set; }
    public string Ad { get; set; } = string.Empty;
    public DateTime KonaklamaBaslangicTarihi { get; set; }
    public DateTime KonaklamaBitisTarihi { get; set; }
    public List<KampBasvuruTesisSecenekDto> Tesisler { get; set; } = [];
}

public class KampBasvuruTesisSecenekDto
{
    public int TesisId { get; set; }
    public string TesisAd { get; set; } = string.Empty;
    public int ToplamKontenjan { get; set; }
    public List<KampKonaklamaBirimiSecenekDto> Birimler { get; set; } = [];
}

public class KampKonaklamaBirimiSecenekDto
{
    public string Kod { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public int MinimumKisi { get; set; }
    public int MaksimumKisi { get; set; }
}
