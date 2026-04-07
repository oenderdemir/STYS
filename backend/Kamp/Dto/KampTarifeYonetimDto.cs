namespace STYS.Kamp.Dto;

public class KampTarifeYonetimBaglamDto
{
    public List<KampProgramiSecenekDto> Programlar { get; set; } = [];
}

public class KampTarifeKaydetRequestDto
{
    public List<KampKonaklamaTarifeYonetimDto> Tarifeler { get; set; } = [];
}
