namespace STYS.Rezervasyonlar.Dto;

public class RezervasyonUzatmaSecenegiDto
{
    public string SenaryoKodu { get; set; } = string.Empty;

    /// <summary>Bkz. RezervasyonUzatmaSenaryoTipleri.</summary>
    public string SenaryoTipi { get; set; } = string.Empty;

    public string Aciklama { get; set; } = string.Empty;

    public int OdaDegisimSayisi { get; set; }

    public decimal EkBazUcret { get; set; }

    public decimal EkNihaiUcret { get; set; }

    public string ParaBirimi { get; set; } = "TRY";

    public string FiyatlamaTipi { get; set; } = string.Empty;

    /// <summary>Orijinal donemde uygulanmis bir indirimin bu uzatma tutarina otomatik yansitilmadigi
    /// gibi durumlarda kullaniciya gosterilecek acik uyari metni; sorun yoksa null.</summary>
    public string? FiyatUyarisi { get; set; }

    public List<KonaklamaSenaryoSegmentDto> Segmentler { get; set; } = [];
}
