namespace STYS.Raporlar.KonaklamaKisiSayisi.Dto;

public class KonaklamaKisiSayisiYilSatiriDto
{
    public int Yil { get; set; }

    public List<KonaklamaKisiSayisiHucreDto> Hucreler { get; set; } = [];

    public int ToplamKisiSayisi { get; set; }
}
