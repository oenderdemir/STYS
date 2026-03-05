using STYS.Fiyatlandirma.Dto;

namespace STYS.Rezervasyonlar.Dto;

public class SenaryoFiyatHesaplamaSonucuDto
{
    public decimal ToplamBazUcret { get; set; }

    public decimal ToplamNihaiUcret { get; set; }

    public string ParaBirimi { get; set; } = "TRY";

    public List<UygulananIndirimDto> UygulananIndirimler { get; set; } = [];
}
