namespace STYS.Raporlar.KonaklamaKisiSayisi.Dto;

public class KonaklamaKisiSayisiRaporDto
{
    public int TesisId { get; set; }

    public string? TesisAdi { get; set; }

    public int Ay { get; set; }

    public string AyAdi { get; set; } = "";

    public int BaslangicYil { get; set; }

    public int BitisYil { get; set; }

    public string Baslik { get; set; } = "";

    public List<KonaklamaKisiSayisiOdaDto> Odalar { get; set; } = [];

    public List<KonaklamaKisiSayisiYilSatiriDto> Yillar { get; set; } = [];
}
