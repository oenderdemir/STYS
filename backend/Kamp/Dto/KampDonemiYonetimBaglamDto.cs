namespace STYS.Kamp.Dto;

public class KampDonemiYonetimBaglamDto
{
    public bool GlobalDonemYonetimiYapabilirMi { get; set; }

    public List<KampProgramiSecenekDto> Programlar { get; set; } = [];

    public List<KampTesisDto> Tesisler { get; set; } = [];
}

public class KampProgramiSecenekDto
{
    public int Id { get; set; }

    public string Ad { get; set; } = string.Empty;

    public int Yil { get; set; }
}

public class KampTesisDto
{
    public int Id { get; set; }

    public string Ad { get; set; } = string.Empty;
}
