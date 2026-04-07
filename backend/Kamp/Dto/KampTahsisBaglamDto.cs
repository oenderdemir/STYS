namespace STYS.Kamp.Dto;

public class KampTahsisBaglamDto
{
    public List<KampTahsisDonemSecenekDto> Donemler { get; set; } = [];

    public List<KampTahsisTesisSecenekDto> Tesisler { get; set; } = [];

    public List<string> Durumlar { get; set; } = [];
}

public class KampTahsisDonemSecenekDto
{
    public int Id { get; set; }

    public string? KampProgramiAd { get; set; }

    public string Ad { get; set; } = string.Empty;
}

public class KampTahsisTesisSecenekDto
{
    public int Id { get; set; }

    public string Ad { get; set; } = string.Empty;
}
