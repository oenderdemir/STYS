namespace STYS.Rezervasyonlar.Dto;

public class RezervasyonOdemeDto
{
    public int Id { get; set; }

    public DateTime OdemeTarihi { get; set; }

    public decimal OdemeTutari { get; set; }

    public string ParaBirimi { get; set; } = "TRY";

    public string OdemeTipi { get; set; } = string.Empty;

    public string? Aciklama { get; set; }

    public string Durum { get; set; } = "Aktif";

    public DateTime? IptalTarihi { get; set; }

    public string? IptalAciklama { get; set; }

    public int? KasaBankaHesapId { get; set; }

    public string? KasaBankaHesapAdi { get; set; }

    public int? TahsilatOdemeBelgesiId { get; set; }

    public string? TahsilatOdemeBelgesiNo { get; set; }
}

