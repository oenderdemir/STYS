namespace STYS.Fiyatlandirma.Dto;

public class OdaFiyatHesaplamaSonucuDto
{
    public decimal BazFiyat { get; set; }

    public decimal NihaiFiyat { get; set; }

    public string ParaBirimi { get; set; } = "TRY";

    public List<UygulananIndirimDto> UygulananIndirimler { get; set; } = [];
}
