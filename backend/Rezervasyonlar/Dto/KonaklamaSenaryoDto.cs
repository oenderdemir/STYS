namespace STYS.Rezervasyonlar.Dto;

public class KonaklamaSenaryoDto
{
    public string SenaryoKodu { get; set; } = string.Empty;

    public string Aciklama { get; set; } = string.Empty;

    public int ToplamOdaSayisi { get; set; }

    public int OdaDegisimSayisi { get; set; }

    public decimal ToplamBazUcret { get; set; }

    public decimal ToplamNihaiUcret { get; set; }

    public string ParaBirimi { get; set; } = "TRY";

    public List<KonaklamaSenaryoSegmentDto> Segmentler { get; set; } = [];
}
